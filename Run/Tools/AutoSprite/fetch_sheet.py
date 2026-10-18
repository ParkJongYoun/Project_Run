import argparse
import json
from pathlib import Path
from urllib.request import urlopen
from race_generation import call

parser=argparse.ArgumentParser()
parser.add_argument('--credential-file',required=True)
parser.add_argument('--sheet-id',required=True)
parser.add_argument('--output',required=True)
args=parser.parse_args()
result=call('get_spritesheet',{'spritesheetId':args.sheet_id},args.credential_file)['spritesheet']
path=Path(args.output)
path.parent.mkdir(parents=True,exist_ok=True)
for key,extension in [('sheetUrl','.png'),('atlasUrl','.json')]:
    with urlopen(result[key],timeout=90) as stream:
        path.with_suffix(extension).write_bytes(stream.read())
path.with_suffix('.source.json').write_text(json.dumps({k:v for k,v in result.items() if not k.endswith('Url')},indent=2),encoding='utf-8')
print({k:v for k,v in result.items() if k in ['id','kind','sourceVideoId','frameWidth','frameHeight','frameCount']})
