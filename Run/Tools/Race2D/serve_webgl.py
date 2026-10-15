"""Loopback-only review server with correct Unity Brotli/gzip content headers."""
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
from functools import partial
import mimetypes
import sys

root=Path(sys.argv[1]).resolve()
port=int(sys.argv[2]) if len(sys.argv)>2 else 8768
mimetypes.add_type('application/wasm','.wasm')
mimetypes.add_type('application/javascript','.js')
mimetypes.add_type('application/octet-stream','.data')

class Handler(SimpleHTTPRequestHandler):
    def guess_type(self,path):
        if path.endswith(('.br','.gz')):path=path.rsplit('.',1)[0]
        return super().guess_type(path)
    def end_headers(self):
        if self.path.split('?',1)[0].endswith('.br'):self.send_header('Content-Encoding','br')
        elif self.path.split('?',1)[0].endswith('.gz'):self.send_header('Content-Encoding','gzip')
        self.send_header('Cache-Control','no-cache')
        super().end_headers()

ThreadingHTTPServer(('127.0.0.1',port),partial(Handler,directory=str(root))).serve_forever()
