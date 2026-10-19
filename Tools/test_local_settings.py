import unittest
from unittest.mock import patch

import local_settings


class BackendSettingsTests(unittest.TestCase):
    def test_origin_normalization(self):
        for value, expected in [
            (' https://backend.example.test/ ', 'https://backend.example.test'),
            ('http://localhost:8080', 'http://localhost:8080'),
        ]:
            with self.subTest(value=value):
                self.assertEqual(local_settings.normalize_backend_url(value), expected)

    def test_invalid_origins(self):
        for value in [
            'ftp://backend.example.test',
            'https://user:password@backend.example.test',
            'https://backend.example.test/api',
            'https://backend.example.test?key=value',
            'https://backend.example.test#section',
            'not-a-url',
            'https://backend.example.test:99999',
        ]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                local_settings.normalize_backend_url(value)

    def test_environment_priority_and_blank_fallback(self):
        with patch.object(local_settings, 'settings', return_value={'backendUrl': 'https://local.example.test/'}):
            with patch.dict(local_settings.os.environ, {'GAME_BACKEND_URL': ' https://override.example.test/ '}):
                self.assertEqual(local_settings.backend_url(), 'https://override.example.test')
            with patch.dict(local_settings.os.environ, {'GAME_BACKEND_URL': ' '}):
                self.assertEqual(local_settings.backend_url(), 'https://local.example.test')


if __name__ == '__main__':
    unittest.main()
