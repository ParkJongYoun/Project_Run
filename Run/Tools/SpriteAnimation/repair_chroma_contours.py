"""Recover original ink contours from retained chroma-key video frames.

This is deterministic matte/color repair, not per-frame image generation.
The input canvas, frame order, timing and all pixels beyond the edge band remain
unchanged. Character-specific search regions and thresholds live in the job JSON.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, gaussian_filter, label


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def repair(image, settings):
    rgb = np.asarray(image.convert('RGB'), dtype=np.float32)
    height, width = rgb.shape[:2]
    border_size = settings['backgroundSampleBorder']
    border = np.concatenate((rgb[:border_size].reshape(-1, 3),
                             rgb[-border_size:].reshape(-1, 3),
                             rgb[:, :border_size].reshape(-1, 3),
                             rgb[:, -border_size:].reshape(-1, 3)))
    background = np.median(border, axis=0)
    excess = np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]
    pink = np.zeros((height, width), dtype=bool)
    for x0, y0, x1, y1 in settings['warmPinkSearchRects']:
        region = np.zeros_like(pink)
        region[y0:y1, x0:x1] = True
        pink |= region & (rgb[:, :, 0] - rgb[:, :, 2] > settings['pinkRedBlueDifference']) & (rgb[:, :, 1] > settings['pinkMinimumGreen'])
    trusted = (excess < settings['trustedMaximumExcess']) | pink
    distance, nearest = distance_transform_edt(~trusted, return_indices=True)
    reference = rgb[nearest[0], nearest[1]]
    reference_excess = np.minimum(reference[:, :, 0], reference[:, :, 2]) - reference[:, :, 1]
    background_excess = min(background[0], background[2]) - background[1]
    coverage = np.clip((background_excess - excess) / np.maximum(background_excess - reference_excess, 1), 0, 1)
    coverage[trusted] = 1
    coverage[(distance > settings['isolatedHazeDistance']) & (coverage < settings['isolatedHazeAlpha'])] = 0
    low = settings['coverageFloor']
    coverage = np.clip((coverage - low) / (1 - low), 0, 1)
    labels, _ = label(coverage > settings['componentAlpha'])
    counts = np.bincount(labels.ravel())
    counts[0] = 0
    bodies = counts[labels] > settings['mainComponentMinimumPixels']
    support = distance_transform_edt(~bodies) < settings['nearBodySupportPixels']
    coverage[~support] = 0
    # A spatially consistent subpixel contour removes chroma-block steps. Do not
    # average displaced frames; that would leave multiple ghost silhouettes.
    smooth = gaussian_filter(coverage, settings['contourSigma'])
    lower, upper = settings['contourCoverageRange']
    alpha = np.clip((smooth - lower) / (upper - lower), 0, 1)
    # Color recovery must use original coverage, not the smoothed silhouette.
    # Otherwise negative RGB after unmixing becomes a green rim when clamped.
    recovered = (rgb - (1 - coverage[:, :, None]) * background) / np.maximum(coverage[:, :, None], .06)
    corrected = np.clip(recovered, 0, 255)
    unreliable = ((recovered.min(axis=2) < -5) | (alpha < .18)) & (alpha > 0)
    corrected[unreliable] = reference[unreliable]
    inside = distance_transform_edt(alpha > .95)
    spill_band = (inside < settings['spillRepairBand']) & (alpha > 0) & ~pink
    spill = np.maximum(np.minimum(corrected[:, :, 0], corrected[:, :, 2]) - corrected[:, :, 1], 0)
    for channel in (0, 2):
        corrected[:, :, channel][spill_band] -= spill[spill_band]
    # Fully covered source interiors are preserved exactly.
    opaque = (alpha >= .999) & (coverage >= .999)
    corrected[opaque] = rgb[opaque]
    distance_inside = distance_transform_edt(alpha > .5)
    outer, transition = settings['inkContourBand']
    ink_weight = np.clip((outer - distance_inside) / transition, 0, 1) * (alpha > 0)
    ink = np.clip(reference * settings['inkReferenceGain'], settings['inkMinimumRgb'], settings['inkMaximumRgb'])
    corrected = corrected * (1 - ink_weight[:, :, None]) + ink * ink_weight[:, :, None]
    alpha[alpha < settings['outputAlphaFloor']] = 0
    rgba = np.dstack((np.clip(corrected, 0, 255), alpha * 255)).round().astype(np.uint8)
    rgba[rgba[:, :, 3] == 0, :3] = 0
    safe_interior = opaque & (distance_inside > settings['interiorVerificationDistance'])
    assert np.array_equal(rgba[:, :, :3][safe_interior], rgb.astype(np.uint8)[safe_interior])
    return Image.fromarray(rgba), {
        'background': background.tolist(),
        'interiorExactPixels': int(safe_interior.sum()),
        'inkRepairPixels': int((ink_weight > 0).sum()),
        'transparentPixels': int((rgba[:, :, 3] == 0).sum()),
        'protectedPinkPixels': int(pink.sum()),
        'edgeTouches': bool(np.any(rgba[0, :, 3]) or np.any(rgba[-1, :, 3]) or np.any(rgba[:, 0, 3]) or np.any(rgba[:, -1, 3])),
        'bounds': Image.fromarray(rgba[:, :, 3]).getbbox(),
    }


def run(config_path):
    config_path = Path(config_path).resolve()
    config = read(config_path)
    metadata_path = (config_path.parent / config['sourceMetadata']).resolve()
    metadata = read(metadata_path)
    output = config_path.parent / config['outputDirectory']
    output.mkdir(exist_ok=False)
    records = []
    previous_alpha = previous_old_alpha = None
    movement, old_movement = [], []
    for frame in metadata['frames']:
        source = metadata_path.parent / frame['file']
        if digest(source) != frame['sha256']:
            raise ValueError('Source hash mismatch: ' + str(source))
        with Image.open(source) as image:
            if list(image.size) != metadata['sourceSize']:
                raise ValueError('Source frame dimensions changed')
            repaired, stats = repair(image, config['matte'])
        name = 'frame_%05d.png' % frame['index']
        repaired.save(output / name)
        alpha = np.asarray(repaired)[:, :, 3].astype(np.float32)
        old = np.asarray(Image.open(config_path.parent / config['previousMatteDirectory'] / name))[:, :, 3].astype(np.float32)
        if previous_alpha is not None:
            movement.append(float(np.mean(np.abs(alpha - previous_alpha))))
            old_movement.append(float(np.mean(np.abs(old - previous_old_alpha))))
        previous_alpha, previous_old_alpha = alpha, old
        stats.update(index=frame['index'], file=name, sourceSha256=frame['sha256'], sha256=digest(output / name), duration=frame['duration'])
        records.append(stats)
        if len(records) % 12 == 0:
            print('Processed %d/%d frames' % (len(records), len(metadata['frames'])), flush=True)
    report = {'algorithm': 'source-ink-contour-recovery-v3', 'configSha256': digest(config_path),
              'toolSha256': digest(__file__), 'sourceSize': metadata['sourceSize'],
              'frameCount': len(records), 'duration': sum(r['duration'] for r in records),
              'sourceHashesVerified': True, 'interiorPixelsPreserved': True,
              'frameGeometryChanged': False, 'frameSelectionChanged': False,
              'temporalAlphaDifferenceMean': float(np.mean(movement)),
              'previousTemporalAlphaDifferenceMean': float(np.mean(old_movement)),
              'temporalMetricMeaning': 'Includes real motion; not a standalone flicker score.',
              'frames': records}
    (output / 'inspection.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: report[k] for k in ('frameCount', 'duration', 'sourceHashesVerified', 'interiorPixelsPreserved')}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('config', type=Path)
    run(parser.parse_args().config)
