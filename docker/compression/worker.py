#!/usr/bin/env python3
"""Optional unprivileged host worker; control is only through a private Unix socket."""
import argparse
import base64
from collections import Counter
import fcntl
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import math
import os
from pathlib import Path
import queue
import re
import shutil
import signal
import socket
from socketserver import ThreadingMixIn
import stat
import subprocess
import tempfile
import threading
import time
import urllib.request
import uuid
import xml.etree.ElementTree as ET


ACTIVE = {'queued', 'running', 'cancelling'}


def probe(path):
    result = subprocess.run(['ffprobe', '-v', 'error', '-show_format', '-show_streams',
                             '-show_chapters', '-of', 'json', str(path)],
                            capture_output=True, check=True, timeout=60)
    return json.loads(result.stdout)


def duration(info):
    value = float(info['format']['duration'])
    if not math.isfinite(value) or value <= 0:
        raise ValueError('Video duration is unavailable; original kept.')
    return value


def video(info):
    return next(s for s in info['streams'] if s.get('codec_type') == 'video'
                and not s.get('disposition', {}).get('attached_pic'))


def validate(source, output, expected_codec):
    if abs(duration(source) - duration(output)) > max(1, duration(source) * .005):
        raise ValueError('Output duration differs; original kept.')
    old, new = video(source), video(output)
    if new.get('codec_name') != expected_codec or any(old.get(k) != new.get(k) for k in ('width', 'height')):
        raise ValueError('Output codec or dimensions differ; original kept.')
    # Audio, subtitles, attachments, dispositions, languages and chapters must survive.
    def signature(info):
        return Counter((s.get('codec_type'), s.get('codec_name') if s.get('codec_type') != 'video' else '',
                        json.dumps(s.get('disposition', {}), sort_keys=True),
                        s.get('tags', {}).get('language', 'und')) for s in info['streams'])
    if signature(source) != signature(output) or len(source.get('chapters', [])) != len(output.get('chapters', [])):
        raise ValueError('Output lost streams, languages, dispositions or chapters; original kept.')


def identity(path):
    s = path.stat()
    return s.st_dev, s.st_ino, s.st_size, s.st_mtime_ns


def supported_file(path, series_path, roots):
    # Resolve symlinks before any I/O. Only registered files inside their series and a mounted root.
    resolved = path.resolve(strict=True)
    parent = series_path.resolve(strict=True)
    if not resolved.is_relative_to(parent) or not any(parent.is_relative_to(r.resolve()) and parent != r.resolve() for r in roots):
        raise ValueError('Episode path is outside its series or configured media roots.')
    if not resolved.is_file() or path.is_symlink():
        raise ValueError('Episode path is not a regular file.')
    return resolved


def script_settings(name):
    path = Path('/usr/local/bin') / name
    if not path.is_file():
        return None
    text = path.read_text()
    def setting(key, fallback):
        match = re.search(r'^' + key + r'=["\']?([a-zA-Z0-9_-]+)', text, re.M)
        return match.group(1) if match else fallback
    result = {'minSizeMb': int(setting('MIN_SIZE_MB', '1000'))}
    if name == 'filesaver':
        result.update(crf=int(setting('CRF', '23')), preset=setting('PRESET', 'slow'))
        if result['preset'] not in {'ultrafast', 'superfast', 'veryfast', 'faster', 'fast', 'medium', 'slow', 'slower', 'veryslow'}:
            return None
    else:
        result['qp'] = int(setting('QP', '23'))
    if not 0 <= result.get('crf', result.get('qp', 23)) <= 51 or not 0 <= result['minSizeMb'] <= 100000:
        return None
    return result


class Worker:
    def __init__(self, config):
        self.config = config
        self.roots = [Path(p) for p in config['mediaRoots']]
        self.state = Path(config['stateDirectory'])
        self.state.mkdir(parents=True, exist_ok=True)
        self.lock = threading.RLock()
        self.jobs = {}
        self.cancelled = set()
        self.work = queue.Queue()
        self.process = None
        self.cpu = self.cpu_name()
        self.threads = max(1, min(2, (os.cpu_count() or 2) - 1))
        self.hardware = self.test_hardware()
        self.modes = self.make_modes()
        self.index = Path(config.get('scriptIndex', '/home/jellyfin/.ghost_index'))
        self.index.parent.mkdir(parents=True, exist_ok=True)
        self.history = {}
        self.load_history()
        for p in self.state.glob('job-*.json'):
            job = json.loads(p.read_text())
            if job['status'] in ACTIVE:
                job.update(status='interrupted', message='Worker restarted. Completed files were kept; start again to process the rest.', currentFile='', filePercent=0)
                self.save(job)
            self.jobs[job['seriesId']] = job
        threading.Thread(target=self.run, daemon=True).start()

    @staticmethod
    def cpu_name():
        try:
            return next(line.split(':', 1)[1].strip() for line in Path('/proc/cpuinfo').read_text().splitlines() if line.startswith('model name'))
        except (OSError, StopIteration):
            return 'Server CPU'

    @staticmethod
    def test_hardware():
        if not Path('/dev/dri/renderD128').exists():
            return False
        try:
            for quality, qp, bf in ((8, 24, 0), (1, 23, 2)):
                subprocess.run(['ffmpeg', '-nostdin', '-v', 'error', '-vaapi_device', '/dev/dri/renderD128',
                                '-f', 'lavfi', '-i', 'color=size=128x128:rate=24', '-t', '0.3',
                                '-vf', 'format=nv12,hwupload', '-c:v', 'h264_vaapi', '-quality', str(quality),
                                '-qp', str(qp), '-bf', str(bf), '-f', 'null', '-'],
                               env={**os.environ, 'LIBVA_DRIVER_NAME': 'i965'}, capture_output=True, check=True, timeout=20)
            return True
        except (OSError, subprocess.SubprocessError):
            return False

    def make_modes(self):
        modes = [
            {'id': 'software-fast', 'name': 'Software • Fast', 'description': 'HEVC with a fast CPU preset. Good space savings; leaves CPU capacity for Plex.', 'codec': 'hevc', 'encoder': 'libx265', 'preset': 'fast', 'crf': 24, 'minSizeMb': 0, 'available': True},
            {'id': 'software-slow', 'name': 'Software • Slow', 'description': 'HEVC with a slow CPU preset. Better compression efficiency; can take hours per episode on this CPU.', 'codec': 'hevc', 'encoder': 'libx265', 'preset': 'slow', 'crf': 24, 'minSizeMb': 0, 'available': True},
            {'id': 'hardware-fast', 'name': 'Hardware • Fast', 'description': 'Intel H.264, speed priority. Lowest CPU load; savings depend on the original file.', 'codec': 'h264', 'encoder': 'h264_vaapi', 'quality': 8, 'qp': 24, 'bf': 0, 'minSizeMb': 0, 'available': self.hardware},
            {'id': 'hardware-slow', 'name': 'Hardware • Slow', 'description': 'Intel H.264, quality priority. More encoder work for better efficiency; still much faster than CPU HEVC.', 'codec': 'h264', 'encoder': 'h264_vaapi', 'quality': 1, 'qp': 23, 'bf': 2, 'minSizeMb': 0, 'available': self.hardware},
        ]
        for name in ('filesaver', 'phantom'):
            try:
                settings = script_settings(name)
            except (OSError, ValueError):
                settings = None
            if settings:
                modes.append({'id': name, 'name': name.title() + ' • Existing preset',
                              'description': ('Uses the settings from your installed script, with Sonarr progress and stronger file validation. '
                                              f"Skips files below {settings['minSizeMb']} MB."),
                              'codec': 'h264', 'encoder': 'libx264' if name == 'filesaver' else 'h264_vaapi',
                              'available': name == 'filesaver' or self.hardware, **settings})
        return modes

    def api(self, path, body=None):
        key = ET.parse(self.config['sonarrConfig']).getroot().findtext('ApiKey')
        request = urllib.request.Request(self.config['sonarrUrl'].rstrip('/') + '/api/v5/' + path,
                                         data=json.dumps(body).encode() if body is not None else None,
                                         headers={'X-Api-Key': key, 'Content-Type': 'application/json'})
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)

    def save(self, job):
        dest = self.state / f"job-{job['seriesId']}.json"
        temp = dest.with_suffix('.tmp')
        temp.write_text(json.dumps(job))
        os.replace(temp, dest)

    def update(self, job, **values):
        with self.lock:
            job.update(values)
            self.save(job)

    def load_history(self):
        path = self.state / 'processed.json'
        self.history = json.loads(path.read_text()) if path.exists() else {}

    def indexed(self, path, info):
        key = str(path)
        if self.history.get(key) == list(identity(path)):
            return True
        if self.index.exists():
            encoded = base64.b64encode(key.encode()).decode()
            s = path.stat()
            for line in self.index.read_text().splitlines():
                fields = line.split('\t')
                if len(fields) == 4 and fields == [encoded, str(int(s.st_mtime)), str(s.st_size), video(info)['codec_name']]:
                    return True
                legacy = line.rsplit('|', 1)
                if len(legacy) == 2 and legacy[0] == key and legacy[1].isdigit() and abs(int(legacy[1]) - math.ceil(s.st_size / 1048576)) <= 2:
                    return True
        return False

    def mark_processed(self, path, codec):
        self.history[str(path)] = list(identity(path))
        temp = self.state / 'processed.tmp'
        temp.write_text(json.dumps(self.history))
        os.replace(temp, self.state / 'processed.json')
        s = path.stat()
        with self.index.open('a') as stream:
            stream.write(f"{base64.b64encode(str(path).encode()).decode()}\t{int(s.st_mtime)}\t{s.st_size}\t{codec}\n")

    def start(self, series_id, body):
        mode = next((m for m in self.modes if m['id'] == body.get('mode') and m['available']), None)
        minimum = body.get('minSizeMb', mode['minSizeMb'] if mode else 0)
        if mode is None or type(minimum) is not int or not 0 <= minimum <= 100000:
            raise ValueError('Choose an available compression preset and a valid minimum file size.')
        series = self.api(f'series/{series_id}')
        files = self.api(f'episodefile?seriesId={series_id}')
        if not files:
            raise ValueError('This show has no imported episode files to compress.')
        with self.lock:
            if self.jobs.get(series_id, {}).get('status') in ACTIVE:
                raise ValueError('This show already has a compression job.')
            if sum(j['status'] in ACTIVE for j in self.jobs.values()) >= 10:
                raise ValueError('The queue is full. Wait for a compression job to finish.')
            job = {'id': uuid.uuid4().hex, 'seriesId': series_id, 'title': series['title'], 'seriesPath': series['path'], 'mode': mode['id'],
                   'modeName': mode['name'], 'minSizeMb': minimum, 'status': 'queued', 'phase': 'queued',
                   'createdAt': time.time(), 'totalFiles': len(files), 'completedFiles': 0, 'compressedFiles': 0,
                   'skippedFiles': 0, 'failedFiles': 0, 'savedBytes': 0, 'percent': 0, 'filePercent': 0,
                   'currentFile': '', 'message': 'Queued. One show is compressed at a time.', 'results': [], 'rescanPending': False}
            self.cancelled.discard(series_id)
            self.jobs[series_id] = job
            self.save(job)
            self.work.put((job, mode, series['path'], files))
            return job

    def cancel(self, series_id):
        with self.lock:
            job = self.jobs.get(series_id)
            if job and job['status'] in ACTIVE:
                self.cancelled.add(series_id)
                self.update(job, status='cancelling', message='Stopping. The active original file will be kept.')
            return job

    def is_cancelled(self, job):
        return job['seriesId'] in self.cancelled

    def run(self):
        while True:
            job, mode, series_path, files = self.work.get()
            try:
                if self.is_cancelled(job):
                    self.update(job, status='cancelled', message='Cancelled before starting.')
                    continue
                # Also coordinate with the user's existing FileSaver / Phantom CLI scripts.
                with Path(str(self.index) + '.lock').open('a') as lock_file:
                    try:
                        fcntl.flock(lock_file, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    except BlockingIOError:
                        raise ValueError('FileSaver or Phantom is already running. Try again after it finishes.')
                    self.update(job, status='running', phase='preparing', message='Preparing episode files.')
                    for item in files:
                        if self.is_cancelled(job):
                            break
                        # Registered file IDs are rechecked just before processing to handle renames/imports.
                        try:
                            current_series = self.api(f"series/{job['seriesId']}")
                            current_file = self.api(f"episodefile/{item['id']}")
                            if current_file['seriesId'] != job['seriesId'] or current_series['path'] != series_path:
                                raise ValueError('Show moved or file association changed; original kept.')
                            path = supported_file(Path(current_file['path']), Path(series_path), self.roots)
                            self.update(job, currentFile=path.name, filePercent=0, phase='preparing')
                            result = self.compress(job, mode, path)
                        except Exception as error:
                            result = {'file': item.get('relativePath', 'Episode'), 'status': 'failed', 'reason': str(error)[:300], 'savedBytes': 0}
                        if self.is_cancelled(job):
                            break
                        with self.lock:
                            job['results'].append(result)
                            job[result['status'] + 'Files'] += 1
                            job['completedFiles'] += 1
                            job['savedBytes'] += result['savedBytes']
                            self.update(job, percent=round(job['completedFiles'] / job['totalFiles'] * 100, 1))
                status = 'cancelled' if self.is_cancelled(job) else ('completedWithErrors' if job['failedFiles'] else 'completed')
                self.update(job, status=status, phase='finished', filePercent=0, currentFile='',
                            message='Cancelled. Finished files were kept; the active original was preserved.' if status == 'cancelled' else 'Compression finished.',
                            finishedAt=time.time())
            except Exception as error:
                self.update(job, status='failed', phase='finished', currentFile='', message=str(error)[:300], finishedAt=time.time())
            finally:
                # A rescan updates sizes and codec information without a metadata refresh or search.
                if job['compressedFiles']:
                    try:
                        self.api('command', {'name': 'RescanSeries', 'seriesId': job['seriesId']})
                    except Exception:
                        self.update(job, rescanPending=True, message=job['message'] + ' Use Refresh & Scan to update Sonarr file sizes.')
                self.work.task_done()

    def command(self, mode, path, output):
        args = ['ffmpeg', '-nostdin', '-hide_banner', '-v', 'error', '-y', '-threads', str(self.threads)]
        if mode['encoder'] == 'h264_vaapi':
            args += ['-vaapi_device', '/dev/dri/renderD128']
        args += ['-i', str(path), '-map', '0', '-map_metadata', '0', '-map_chapters', '0', '-c', 'copy', '-c:v:0', mode['encoder']]
        if mode['encoder'] == 'h264_vaapi':
            args += ['-filter:v:0', 'format=nv12,hwupload', '-qp', str(mode['qp'])]
            if 'quality' in mode:
                args += ['-quality', str(mode['quality']), '-bf', str(mode['bf'])]
        else:
            args += ['-preset', mode['preset'], '-crf', str(mode['crf']), '-threads:v:0', str(self.threads)]
            if mode['encoder'] == 'libx265':
                args += ['-x265-params', f'pools={self.threads}:frame-threads=1:log-level=error']
        args += ['-progress', 'pipe:1', '-nostats', str(output)]
        return ['nice', '-n', '15', 'ionice', '-c', '2', '-n', '7'] + args

    def process_file(self, job, args, duration_seconds, error_log, phase):
        progress = queue.Queue()
        with error_log.open('wb') as errors:
            process = subprocess.Popen(args, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=errors,
                                       start_new_session=True, env={**os.environ, 'LIBVA_DRIVER_NAME': 'i965'})
            def read_progress():
                for line in process.stdout:
                    progress.put(line.decode(errors='replace').strip())
            threading.Thread(target=read_progress, daemon=True).start()
            try:
                last_save = 0
                file_percent = 0
                while process.poll() is None:
                    if self.is_cancelled(job):
                        raise InterruptedError('Cancelled; original kept.')
                    try:
                        line = progress.get(timeout=.25)
                        if line.startswith('out_time_us='):
                            value = line.split('=', 1)[1]
                            if value.isdigit():
                                file_percent = min(100, int(value) / (duration_seconds * 10000))
                    except queue.Empty:
                        pass
                    if time.monotonic() - last_save > 1:
                        # Validation is a separate phase and takes the final 10% of the file bar.
                        fraction = file_percent * .9 if phase == 'encoding' else 90 + file_percent * .1
                        self.update(job, phase=phase, filePercent=round(fraction, 1),
                                    percent=round((job['completedFiles'] + fraction / 100) / job['totalFiles'] * 100, 1),
                                    message='Compressing current episode.' if phase == 'encoding' else 'Checking the complete output before replacement.')
                        last_save = time.monotonic()
                if process.returncode or (phase == 'validating' and error_log.stat().st_size):
                    raise ValueError('Encoder or validation failed; original kept. ' + error_log.read_text(errors='replace')[-250:])
                if self.is_cancelled(job):
                    raise InterruptedError('Cancelled; original kept.')
            finally:
                if process.poll() is None:
                    os.killpg(process.pid, signal.SIGTERM)
                    try:
                        process.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid, signal.SIGKILL)
                        process.wait()
                process.stdout.close()

    def compress(self, job, mode, path):
        result = {'file': path.name, 'status': 'skipped', 'reason': '', 'savedBytes': 0}
        original_stat = path.stat()
        before = identity(path)
        info = probe(path)
        stream = video(info)
        reason = None
        if path.suffix.lower() not in {'.mkv', '.mp4', '.m4v'}:
            reason = 'Container is not supported.'
        elif original_stat.st_size < job['minSizeMb'] * 1048576:
            reason = 'Below the selected minimum file size.'
        elif stream['codec_name'] in {'hevc', 'av1'}:
            reason = 'Already encoded with an efficient codec.'
        elif (stream.get('color_transfer') in {'smpte2084', 'arib-std-b67'}
              or re.search(r'(?:p|gray)(?:9|10|12|14|16)(?:le|be)?$|(?:rgb|bgr)48|p0(?:10|16)', stream.get('pix_fmt', ''))
              or any('mastering display' in s.get('side_data_type', '').lower()
                     or 'dovi' in s.get('side_data_type', '').lower() for s in stream.get('side_data_list', []))):
            reason = 'HDR or high bit-depth video is preserved.'
        elif len([s for s in info['streams'] if s.get('codec_type') == 'video']) != 1:
            reason = 'Multiple video tracks or embedded cover art are preserved.'
        elif self.indexed(path, info):
            reason = 'Already processed by Sonarr, FileSaver or Phantom.'
        elif shutil.disk_usage(path.parent).free < original_stat.st_size * 1.1 + 67108864:
            reason = 'Not enough temporary disk space.'
        if reason:
            return {**result, 'reason': reason}
        with tempfile.TemporaryDirectory(prefix='.sonarr-compression-', dir=path.parent) as temp:
            output = Path(temp) / ('output' + path.suffix)
            errors = Path(temp) / 'encode.log'
            self.process_file(job, self.command(mode, path, output), duration(info), errors, 'encoding')
            size = output.stat().st_size
            if size <= 0 or size >= original_stat.st_size * .98:
                return {**result, 'reason': 'Output did not save at least 2%; original kept.'}
            validate(info, probe(output), mode['codec'])
            self.process_file(job, ['nice', '-n', '15', 'ffmpeg', '-nostdin', '-v', 'error', '-xerror', '-threads', str(self.threads),
                                   '-i', str(output), '-map', '0:v:0', '-map', '0:a?', '-progress', 'pipe:1', '-f', 'null', '-'],
                              duration(info), Path(temp) / 'validate.log', 'validating')
            # Recheck both the live Sonarr association and source identity at the commit boundary.
            live = self.api(f"series/{job['seriesId']}")
            if live['path'] != job['seriesPath']:
                raise ValueError('Series path changed; original kept.')
            registered = self.api(f"episodefile?seriesId={job['seriesId']}")
            if not any(Path(f['path']) == path for f in registered) or identity(path) != before or self.is_cancelled(job):
                raise ValueError('Source or file association changed; original kept.')
            os.chmod(output, stat.S_IMODE(original_stat.st_mode))
            os.chown(output, original_stat.st_uid, original_stat.st_gid)
            os.utime(output, ns=(original_stat.st_atime_ns, original_stat.st_mtime_ns))
            with output.open('rb') as handle:
                os.fsync(handle.fileno())
            # Atomic rename preserves hard-linked torrent originals and never exposes a partial file.
            os.replace(output, path)
            self.mark_processed(path, mode['codec'])
            return {**result, 'status': 'compressed', 'savedBytes': original_stat.st_size - size, 'reason': 'Validated and replaced.'}


class UnixServer(ThreadingMixIn, HTTPServer):
    address_family = socket.AF_UNIX
    daemon_threads = True

    def server_bind(self):
        self.socket.bind(self.server_address)
        self.server_name, self.server_port = 'compression', 0


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def reply(self, status, payload):
        data = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def handle_request(self):
        worker = self.server.worker
        try:
            if self.command == 'GET' and self.path == '/capabilities':
                self.reply(200, {'available': True, 'cpu': worker.cpu, 'threads': worker.threads,
                                 'hardware': 'Intel Haswell H.264 / VAAPI' if worker.hardware else 'Hardware encoder unavailable',
                                 'modes': worker.modes})
                return
            match = re.fullmatch(r'/series/([1-9][0-9]*)', self.path)
            if not match:
                self.reply(404, {'message': 'Unknown compression endpoint.'})
                return
            series_id = int(match.group(1))
            if self.command == 'POST':
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 4096:
                    raise ValueError('Invalid request size.')
                body = json.loads(self.rfile.read(size))
                if not isinstance(body, dict):
                    raise ValueError('Invalid compression request.')
                worker.start(series_id, body)
            elif self.command == 'DELETE':
                worker.cancel(series_id)
            with worker.lock:
                self.reply(200, {'available': True, 'job': worker.jobs.get(series_id)})
        except (ValueError, KeyError, urllib.error.HTTPError) as error:
            self.reply(400, {'message': str(error)[:300]})
        except Exception:
            self.reply(503, {'message': 'Compression worker could not complete the request. Check the worker service.'})

    do_GET = handle_request
    do_POST = handle_request
    do_DELETE = handle_request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', required=True)
    args = parser.parse_args()
    config = json.loads(Path(args.config).read_text())
    worker = Worker(config)
    path = Path(config['socket'])
    path.parent.mkdir(parents=True, exist_ok=True)
    path.unlink(missing_ok=True)
    server = UnixServer(str(path), Handler)
    server.worker = worker
    os.chmod(path, 0o660)
    server.serve_forever()


if __name__ == '__main__':
    main()
