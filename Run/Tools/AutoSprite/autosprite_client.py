"""Run-local AutoSprite client. Reads an external credential only in memory."""
import argparse
import json
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import HTTPError

BASE = 'https://www.autosprite.io'

def request(method, path, payload=None, credential_file=None):
    if not path.startswith('/api/'):
        raise ValueError('Only official AutoSprite API paths accept credentials')
    headers = {'Accept': 'application/json, text/event-stream', 'Content-Type': 'application/json'}
    if credential_file:
        key = Path(credential_file).read_text(encoding='utf-8-sig').strip()
        headers['Authorization' if path == '/api/mcp' else 'x-api-key'] = ('Bearer ' if path == '/api/mcp' else '') + key
    body = json.dumps(payload).encode() if payload is not None else None
    try:
        with urlopen(Request(BASE + path, data=body, headers=headers, method=method), timeout=90) as response:
            raw = response.read().decode()
    except HTTPError as error:
        detail = error.read().decode(errors='replace')
        if credential_file:
            detail = detail.replace(key, '[redacted]')
        raise RuntimeError('AutoSprite HTTP %s: %s' % (error.code, detail[:2000])) from None
    if raw.lstrip().startswith(('event:', 'data:')):
        messages = [json.loads(line[5:].strip()) for line in raw.splitlines() if line.startswith('data:')]
        return messages[-1]
    return json.loads(raw)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--credential-file', required=True)
    parser.add_argument('--path', default='/api/mcp')
    parser.add_argument('--method', default='POST')
    parser.add_argument('--input')
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    payload = json.loads(Path(args.input).read_text(encoding='utf-8-sig')) if args.input else {'jsonrpc':'2.0','id':1,'method':'tools/list','params':{}}
    result = request(args.method, args.path, payload if args.method != 'GET' else None, args.credential_file)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Saved API response:', output)

if __name__ == '__main__':
    main()
