import json, time
from datetime import datetime, timezone
from pathlib import Path
import websocket

# Read only this task's submission socket; no queue, model or workflow mutations.
log=Path(__file__).with_name('progress.jsonl')
ws=websocket.create_connection('ws://127.0.0.1:8188/ws?clientId=53a87090-d7ba-4072-8811-9cae36fe7a4e',timeout=5)
until=time.monotonic()+7200
try:
    while time.monotonic()<until:
        try: raw=ws.recv()
        except websocket.WebSocketTimeoutException: continue
        if not isinstance(raw,str):continue
        message=json.loads(raw)
        if message.get('type') not in ['executing','progress','progress_state','execution_error','execution_success','execution_cached']:continue
        message['observed_utc']=datetime.now(timezone.utc).isoformat()
        line=json.dumps(message,ensure_ascii=True)
        print(line,flush=True)
        with log.open('a',encoding='utf-8') as f:f.write(line+'\n')
        if message.get('type') in ['execution_error','execution_success']:break
        if message.get('type')=='executing' and message.get('data',{}).get('node') is None:break
finally:ws.close()
