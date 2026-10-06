"""File safety tests; integration mode only uses generated videos in a temporary folder."""
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch

from worker import Worker, duration, identity, supported_file, validate


def media(codec='h264', seconds='10'):
    return {'format': {'duration': seconds}, 'chapters': [{'id': 0}], 'streams': [
        {'codec_type': 'video', 'codec_name': codec, 'width': 1920, 'height': 1080, 'disposition': {'default': 1}},
        {'codec_type': 'audio', 'codec_name': 'aac', 'tags': {'language': 'eng'}, 'disposition': {'default': 1}},
        {'codec_type': 'subtitle', 'codec_name': 'subrip', 'tags': {'language': 'eng'}, 'disposition': {'forced': 1}}]}


class SafetyTests(unittest.TestCase):
    def test_reject_truncated_output(self):
        with self.assertRaises(ValueError):
            validate(media(), media('hevc', '8'), 'hevc')

    def test_reject_wrong_codec(self):
        with self.assertRaises(ValueError):
            validate(media(), media(), 'hevc')

    def test_reject_lost_stream(self):
        output = media('hevc')
        output['streams'].pop()
        with self.assertRaises(ValueError):
            validate(media(), output, 'hevc')

    def test_reject_changed_dimensions(self):
        output = media('hevc')
        output['streams'][0]['height'] = 720
        with self.assertRaises(ValueError):
            validate(media(), output, 'hevc')

    def test_reject_lost_language(self):
        output = media('hevc')
        output['streams'][1]['tags']['language'] = 'und'
        with self.assertRaises(ValueError):
            validate(media(), output, 'hevc')

    def test_reject_lost_forced_subtitle(self):
        output = media('hevc')
        output['streams'][2]['disposition']['forced'] = 0
        with self.assertRaises(ValueError):
            validate(media(), output, 'hevc')

    def test_reject_lost_chapter(self):
        output = media('hevc')
        output['chapters'] = []
        with self.assertRaises(ValueError):
            validate(media(), output, 'hevc')

    def test_accept_stream_preserving_encode(self):
        validate(media(), media('hevc'), 'hevc')

    def test_reject_unknown_duration(self):
        for value in ('nan', 'inf', '0', '-1'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                duration(media(seconds=value))

    def test_paths_cannot_escape_registered_series(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            series = root / 'Show'
            series.mkdir()
            outside = root / 'other.mkv'
            outside.write_bytes(b'outside')
            with self.assertRaises(ValueError):
                supported_file(outside, series, [root])
            linked = series / 'linked.mkv'
            linked.symlink_to(outside)
            with self.assertRaises(ValueError):
                supported_file(linked, series, [root])

    def test_media_root_itself_is_not_a_series(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            file = root / 'show.mkv'
            file.write_bytes(b'data')
            with self.assertRaises(ValueError):
                supported_file(file, root, [root])

    def test_missing_file_is_not_processed(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(FileNotFoundError):
                supported_file(Path(directory) / 'missing.mkv', Path(directory), [Path(directory)])


class WorkerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.series = self.root / 'Show'
        self.series.mkdir()
        self.file = self.series / 'Episode.mkv'
        self.file.write_bytes(b'original' * 1000)
        self.worker = Worker.__new__(Worker)
        self.worker.roots = [self.root]
        self.worker.state = self.root / 'state'
        self.worker.state.mkdir()
        self.worker.index = self.root / 'index'
        self.worker.history = {}
        self.worker.lock = threading.RLock()
        self.worker.jobs = {}
        self.worker.cancelled = set()
        self.worker.work = queue.Queue()
        self.worker.threads = 2
        self.worker.hardware = True
        with patch('worker.script_settings', return_value=None):
            self.worker.modes = self.worker.make_modes()
        self.worker.api = lambda path, body=None: (
            {'id': 1, 'path': str(self.series), 'title': 'Show'} if path == 'series/1'
            else [{'id': 1, 'seriesId': 1, 'path': str(self.file), 'relativePath': self.file.name}])
        self.job = self.worker.start(1, {'mode': 'software-fast'})

    def tearDown(self):
        self.temp.cleanup()

    def test_duplicate_show_is_rejected(self):
        with self.assertRaises(ValueError):
            self.worker.start(1, {'mode': 'software-fast'})

    def test_arbitrary_encoder_and_argument_are_rejected(self):
        for request in ({'mode': 'shell'}, {'mode': 'software-fast', 'minSizeMb': ';rm'},
                        {'mode': 'hardware-fast', 'minSizeMb': -1}):
            with self.subTest(request=request), self.assertRaises(ValueError):
                self.worker.start(2, request)

    def test_cancel_is_scoped_to_show(self):
        self.worker.cancel(1)
        self.assertTrue(self.worker.is_cancelled(self.job))
        self.assertEqual(self.worker.jobs[1]['status'], 'cancelling')
        self.assertFalse(self.worker.is_cancelled({'seriesId': 2}))

    def test_completed_identity_only_skips_unchanged_file(self):
        self.worker.mark_processed(self.file, 'h264')
        self.assertTrue(self.worker.indexed(self.file, media()))
        self.file.write_bytes(b'new import')
        self.assertFalse(self.worker.indexed(self.file, media()))

    def test_persistent_state_is_written(self):
        loaded = json.loads((self.worker.state / 'job-1.json').read_text())
        self.assertEqual(loaded['id'], self.job['id'])
        self.assertEqual(loaded['status'], 'queued')

    def test_hdr_is_never_encoded(self):
        hdr = media()
        hdr['streams'][0]['color_transfer'] = 'smpte2084'
        before = self.file.read_bytes()
        with patch('worker.probe', return_value=hdr):
            result = self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(result['status'], 'skipped')
        self.assertEqual(before, self.file.read_bytes())

    def test_efficient_codec_is_never_reencoded(self):
        with patch('worker.probe', return_value=media('hevc')):
            result = self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(result['status'], 'skipped')

    def test_failed_encoder_keeps_original_and_cleans_temp(self):
        before = identity(self.file)
        with patch('worker.probe', return_value=media()), patch.object(self.worker, 'process_file', side_effect=ValueError('encoder failed')):
            with self.assertRaises(ValueError):
                self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(before, identity(self.file))
        self.assertFalse(list(self.series.glob('.sonarr-compression-*')))

    def test_larger_output_is_discarded(self):
        before = self.file.read_bytes()
        def encode(job, args, *rest):
            Path(args[-1]).write_bytes(before + b'larger')
        with patch('worker.probe', return_value=media()), patch.object(self.worker, 'process_file', side_effect=encode):
            result = self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(result['status'], 'skipped')
        self.assertEqual(before, self.file.read_bytes())

    def test_unregistered_output_cannot_replace_original(self):
        before = self.file.read_bytes()
        def encode(job, args, *rest):
            if args[-1] != '-':
                Path(args[-1]).write_bytes(b'smaller')
        self.worker.api = lambda path, body=None: {'path': str(self.series)} if path == 'series/1' else []
        with patch('worker.probe', side_effect=[media(), media('hevc')]), patch.object(self.worker, 'process_file', side_effect=encode):
            with self.assertRaises(ValueError):
                self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(before, self.file.read_bytes())

    def test_source_changed_during_encode_is_preserved(self):
        def encode(job, args, *rest):
            if args[-1] != '-':
                Path(args[-1]).write_bytes(b'smaller')
                self.file.write_bytes(b'replacement from downloader')
        with patch('worker.probe', side_effect=[media(), media('hevc')]), patch.object(self.worker, 'process_file', side_effect=encode):
            with self.assertRaises(ValueError):
                self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(self.file.read_bytes(), b'replacement from downloader')

    def test_cancellation_keeps_source(self):
        before = self.file.read_bytes()
        def encode(job, args, *rest):
            self.worker.cancel(1)
            raise InterruptedError('cancelled')
        with patch('worker.probe', return_value=media()), patch.object(self.worker, 'process_file', side_effect=encode):
            with self.assertRaises(InterruptedError):
                self.worker.compress(self.job, self.worker.modes[0], self.file)
        self.assertEqual(before, self.file.read_bytes())

    def test_command_copies_all_other_streams_and_bounds_threads(self):
        args = self.worker.command(self.worker.modes[0], self.file, self.root / 'output.mkv')
        self.assertIn('pools=2:frame-threads=1:log-level=error', args)
        self.assertEqual(args[args.index('-map') + 1], '0')
        ffmpeg_args = args[args.index('ffmpeg') + 1:]
        self.assertEqual(ffmpeg_args[ffmpeg_args.index('-c') + 1], 'copy')


@unittest.skipUnless(os.environ.get('SONARR_COMPRESSION_INTEGRATION') == '1', 'Requires host FFmpeg; uses generated fixture videos only')
class EncoderIntegrationTests(unittest.TestCase):
    setUp = WorkerTests.setUp
    tearDown = WorkerTests.tearDown

    def test_real_encode_and_hardlink_preservation(self):
        self.worker.hardware = self.worker.test_hardware()
        self.worker.modes = self.worker.make_modes()
        subtitle = self.root / 'subtitles.srt'
        subtitle.write_text('1\n00:00:00,000 --> 00:00:01,500\nCompression fixture\n')
        metadata = self.root / 'chapters.txt'
        metadata.write_text(';FFMETADATA1\n[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=2000\ntitle=Fixture chapter\n')
        for mode in self.worker.modes:
            if not mode['available']:
                continue
            with self.subTest(mode=mode['id']):
                self.worker.history = {}
                self.worker.index.unlink(missing_ok=True)
                subprocess.run(['ffmpeg', '-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i',
                                'testsrc2=size=320x240:rate=24', '-f', 'lavfi', '-i', 'sine=frequency=440',
                                '-f', 'lavfi', '-i', 'sine=frequency=880', '-i', str(subtitle), '-f', 'ffmetadata', '-i', str(metadata),
                                '-map', '0:v', '-map', '1:a', '-map', '2:a', '-map', '3:s', '-map_metadata', '4', '-map_chapters', '4',
                                '-metadata:s:a:0', 'language=eng', '-metadata:s:a:1', 'language=jpn',
                                '-metadata:s:s:0', 'language=eng', '-disposition:a:0', 'default', '-disposition:a:1', '0',
                                '-disposition:s:0', 'forced', '-c:s', 'srt',
                                '-t', '2', '-c:v', 'mpeg2video', '-q:v', '2', '-c:a', 'aac',
                                str(self.file)], check=True)
                link = self.root / 'torrent-original.mkv'
                link.unlink(missing_ok=True)
                os.link(self.file, link)
                original = link.read_bytes()
                self.job.update(minSizeMb=0, status='running')
                result = self.worker.compress(self.job, mode, self.file)
                self.assertEqual(result['status'], 'compressed', result)
                self.assertLess(self.file.stat().st_size, len(original))
                self.assertEqual(link.read_bytes(), original)
                self.assertNotEqual(link.stat().st_ino, self.file.stat().st_ino)
                self.assertFalse(list(self.series.glob('.sonarr-compression-*')))


if __name__ == '__main__':
    unittest.main(verbosity=2)
