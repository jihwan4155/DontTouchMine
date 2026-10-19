"""Machine-specific authoring paths and deployment addresses, excluded from Git."""
import json
import os
from pathlib import Path
from urllib.parse import urlsplit

SETTINGS_PATH = Path(__file__).resolve().parent.parent / '.local' / 'settings.json'

def settings():
    return json.loads(SETTINGS_PATH.read_text(encoding='utf-8')) if SETTINGS_PATH.is_file() else {}

def authoring_path(name):
    try:
        return settings()['authoringPaths'][name]
    except KeyError as failure:
        raise ValueError(f'Configure authoringPaths.{name} in .local/settings.json') from failure

def normalize_backend_url(value):
    url = (value or '').strip()
    parsed = urlsplit(url)
    if (parsed.scheme not in ('http', 'https') or not parsed.hostname
            or parsed.username is not None or parsed.password is not None
            or parsed.path not in ('', '/') or parsed.query or parsed.fragment):
        raise ValueError('Backend URL must be an HTTP or HTTPS origin without credentials, path, query or fragment')
    # Reading port also rejects malformed/out-of-range ports.
    parsed.port
    return url.rstrip('/')

def backend_url():
    value = os.environ.get('GAME_BACKEND_URL')
    if not value or not value.strip():
        value = settings().get('backendUrl')
    if not value:
        raise ValueError('Set GAME_BACKEND_URL or backendUrl in .local/settings.json')
    return normalize_backend_url(value)

if __name__ == '__main__':
    print(backend_url())
