"""Register Run original art and generate eight-direction racing animations."""
import argparse
import base64
import hashlib
import json
import uuid
from pathlib import Path
from urllib.request import urlopen
from autosprite_client import request

def safe_record(value):
    if isinstance(value, dict):
        return {k:safe_record(v) for k,v in value.items() if not k.endswith('Url')}
    if isinstance(value, list):
        return [safe_record(v) for v in value]
    return value

def call(name, arguments, credential):
    response = request('POST', '/api/mcp', {'jsonrpc':'2.0','id':1,'method':'tools/call','params':{'name':name,'arguments':arguments}}, credential)
    if 'error' in response:
        raise RuntimeError(str(response['error']))
    result = response['result']
    if result.get('isError'):
        raise RuntimeError(str(result))
    if 'structuredContent' in result:
        return result['structuredContent']
    return json.loads(next(c['text'] for c in result['content'] if c['type'] == 'text'))

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('operation', choices=['account','upload','generate','poll','sheets','download'])
    parser.add_argument('--credential-file', required=True)
    parser.add_argument('--directory')
    args = parser.parse_args()
    if args.operation == 'account':
        print(json.dumps(call('get_account', {}, args.credential_file)))
        return
    folder = Path(args.directory)
    manifest_path = folder / 'generation.json'
    data = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    if args.operation == 'upload':
        if data.get('characterId'):
            raise RuntimeError('Already registered; refusing duplicate upload')
        original = (folder / 'original.png').read_bytes()
        response = call('upload_character', {'name':data['name'],'isHumanoid':data['isHumanoid'],'characterDescription':data['description'],'imageUrl':'data:image/png;base64,'+base64.b64encode(original).decode()},args.credential_file)
        data['characterId'] = response['character']['id']
        data['sourceSha256'] = hashlib.sha256(original).hexdigest()
        data['registration'] = response
        manifest_path.write_text(json.dumps(safe_record(data), ensure_ascii=False, indent=2),encoding='utf-8')
        with urlopen(response['character']['baseImageUrl'], timeout=60) as stream:
            data['uploadedSha256'] = hashlib.sha256(stream.read()).hexdigest()
        data['uploadUnchanged'] = data['uploadedSha256'] == data['sourceSha256']
        if not data['uploadUnchanged']:
            raise RuntimeError('Registered source differs from original; inspect before generation')
    elif args.operation == 'generate':
        if data.get('jobId'):
            raise RuntimeError('Job exists; poll it rather than buying another')
        data.setdefault('idempotencyKey', 'project-run-' + str(uuid.uuid4()))
        manifest_path.write_text(json.dumps(data, ensure_ascii=False, indent=2),encoding='utf-8')
        response = call('generate_isometric_pack', {'characterId':data['characterId'],'action':'custom','name':'Race Run','prompt':data['motionPrompt'],'loop':True,'frameCount':0,'frameSize':512,'idempotencyKey':data['idempotencyKey']},args.credential_file)
        data['jobId'] = response['jobId']
        data['generation'] = response
    elif args.operation == 'poll':
        response = call('get_job_status', {'jobId':data['jobId']},args.credential_file)
        data['job'] = response
    elif args.operation == 'sheets':
        response = call('list_spritesheets', {'characterId':data['characterId']},args.credential_file)
        data['sheets'] = response
    else:
        response = call('list_spritesheets', {'characterId':data['characterId'],'latestOnly':True,'limit':10},args.credential_file)
        data['sheets'] = response
        download_dir = folder / 'source'
        download_dir.mkdir(exist_ok=True)
        for sheet in response['items']:
            direction = sheet['kind'].rsplit('_',1)[1]
            if direction not in ['up','northeast','right','southeast','down','southwest','left','northwest']:
                raise RuntimeError('Unexpected direction')
            for key, suffix in [('sheetUrl','.png'),('atlasUrl','.json')]:
                target = download_dir / (direction + suffix)
                if not target.exists():
                    with urlopen(sheet[key], timeout=90) as stream:
                        target.write_bytes(stream.read())
        response = {'downloaded':len(data['sheets']['items']),'directory':str(download_dir)}
    manifest_path.write_text(json.dumps(safe_record(data), ensure_ascii=False, indent=2),encoding='utf-8')
    print(json.dumps({key:value for key,value in response.items() if key not in ['zipUrl','character','items']}, ensure_ascii=True, indent=2))
    if 'character' in response:
        print('characterId:', data['characterId'])

if __name__ == '__main__':
    main()
