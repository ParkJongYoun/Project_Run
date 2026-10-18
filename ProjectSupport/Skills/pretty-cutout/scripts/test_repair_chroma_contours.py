"""Check portable inputs and optionally reproduce a retained real-world case."""
import argparse
import copy
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from repair_chroma_contours import digest, read, repair, run


def expect_error(call, error_type):
    try:
        call()
    except error_type:
        return
    raise AssertionError('Expected ' + error_type.__name__)


def fixture(size, shift=0):
    im = Image.new('RGB', size, (253, 2, 250))
    draw = ImageDraw.Draw(im)
    draw.rectangle((20 + shift, 20, 80 + shift, 100), fill=(90, 65, 42))
    draw.rectangle((40 + shift, 38, 58 + shift, 58), fill=(253, 2, 250))
    draw.rectangle((100 + shift, 35, 125 + shift, 60), fill=(220, 50, 120))
    return im


def main(work, case):
    work = work.resolve()
    work.mkdir(parents=True, exist_ok=False)
    base = read(Path(__file__).resolve().parents[1] / 'references/settings.example.json')
    cfg = copy.deepcopy(base['matte'])
    cfg['backgroundSampleBorder'] = 4
    cfg['mainComponentMinimumPixels'] = 3
    cfg['warmPinkSearchRects'] = [[98, 30, 132, 65]]
    for size in ((160, 128), (256, 144)):
        result, _ = repair(fixture(size), cfg)
        rgba = np.asarray(result)
        assert result.size == size
        assert rgba[0, 0, 3] == 0
        assert rgba[48, 48, 3] == 0  # True internal gap stays transparent.
        assert tuple(rgba[30, 30]) == (90, 65, 42, 255)
        assert tuple(rgba[45, 110]) == (220, 50, 120, 255)

    a, _ = repair(fixture((160, 128)), cfg)
    b, _ = repair(fixture((160, 128), 3), cfg)
    assert b.getbbox()[0] - a.getbbox()[0] == 3  # Preserve actual translation.
    expect_error(lambda: repair(Image.new('RGBA', (160, 128)), cfg), ValueError)
    expect_error(lambda: repair(Image.new('RGB', (160, 128), (0, 255, 0)), cfg), ValueError)
    (work / 'raw').mkdir()
    frames = []
    for i, duration in enumerate((.08, .12, .04)):
        source = work / 'raw' / ('source_%02d.png' % i)
        fixture((160, 128), i).save(source)
        frames.append({'index': i, 'file': 'raw/' + source.name,
                       'sha256': digest(source), 'duration': duration})
    metadata = {'sourceSize': [160, 128], 'frames': frames}
    (work / 'frames.json').write_text(json.dumps(metadata), encoding='utf-8')
    config = {'sourceMetadata': 'frames.json', 'outputDirectory': 'batch', 'matte': cfg}
    config_path = work / 'batch.json'
    config_path.write_text(json.dumps(config), encoding='utf-8')
    run(config_path)
    report = read(work / 'batch/inspection.json')
    assert report['frameCount'] == 3 and abs(report['duration'] - .24) < 1e-9
    assert report['previousTemporalAlphaDifferenceMean'] is None
    for frame in frames:
        assert digest(work / frame['file']) == frame['sha256']
    expect_error(lambda: run(config_path), FileExistsError)
    still_config = {'sourceImage': 'raw/source_00.png', 'outputDirectory': 'still', 'matte': cfg}
    (work / 'still.json').write_text(json.dumps(still_config), encoding='utf-8')
    run(work / 'still.json')
    assert (work / 'still/cutout.png').is_file()
    assert read(work / 'still/inspection.json')['duration'] is None
    metadata['frames'][0]['sha256'] = '0' * 64
    (work / 'bad_frames.json').write_text(json.dumps(metadata), encoding='utf-8')
    config.update(sourceMetadata='bad_frames.json', outputDirectory='bad_output')
    (work / 'bad.json').write_text(json.dumps(config), encoding='utf-8')
    expect_error(lambda: run(work / 'bad.json'), ValueError)
    assert not (work / 'bad_output').exists()
    reproduced = []
    if case:
        settings = read(case)
        meta_path = (case.parent / settings['sourceMetadata']).resolve()
        meta = read(meta_path)
        for frame in meta['frames']:
            source = meta_path.parent / frame['file']
            assert digest(source) == frame['sha256']
            actual, _ = repair(Image.open(source), settings['matte'])
            expected = Image.open(case.parent / settings['outputDirectory'] / ('frame_%05d.png' % frame['index']))
            assert np.array_equal(np.asarray(actual), np.asarray(expected))
            reproduced.append(frame['index'])
            if len(reproduced) % 12 == 0:
                print('Reproduced %d/%d retained frames' % (len(reproduced), len(meta['frames'])), flush=True)
    result = {'status': 'passed', 'nonSquareSizes': [[160, 128], [256, 144]],
              'tested': ['opaque interior', 'pink protection', 'internal hole', 'translation',
                         'existing alpha rejection', 'non-magenta rejection', 'variable frame durations',
                         'optional previous matte', 'no overwrite', 'still input', 'source hash mismatch'],
              'realCaseReproducedFrames': reproduced}
    (work / 'validation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'status': 'passed', 'realFrames': len(reproduced), 'report': str(work / 'validation.json')}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, required=True, help='A new validation directory inside the active workspace')
    parser.add_argument('--case', type=Path, help='Existing repair_settings.json with retained final RGBA frames')
    args = parser.parse_args()
    main(args.work, args.case.resolve() if args.case else None)
