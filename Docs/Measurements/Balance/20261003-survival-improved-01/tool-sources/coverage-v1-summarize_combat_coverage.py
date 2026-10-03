"""Summarize a fixed prefix of passive combat coverage, preserving observation limits."""
import argparse
import collections
import hashlib
import json
from pathlib import Path


def summarize(root):
    root = root.resolve()
    source = root / 'combat-coverage.jsonl'
    size, consumed, sequence = source.stat().st_size, 0, 0
    digest = hashlib.sha256()
    actors, days = {}, collections.Counter()
    first, last = None, None
    with source.open('rb') as stream:
        while stream.tell() < size:
            raw = stream.readline(size - stream.tell())
            if not raw.endswith(b'\n'):
                break
            row = json.loads(raw)
            assert row['sequence'] == sequence + 1, 'Coverage sequence gap'
            sequence = row['sequence']
            consumed += len(raw)
            digest.update(raw)
            first = first or row
            last = row
            days[str(row['day'])] += 1
            for actor in row['actors']:
                key = actor['building']
                if key not in actors:
                    actors[key] = dict(definition=actor['definition'], range=actor['range'],
                        kinds=actor['kinds'], firstObservedGameSeconds=row['gameSeconds'],
                        samples=0, kindEligibleSamples=0, permittedEligibleSamples=0,
                        airInRangeSamples=0, groundInRangeSamples=0, minimumNearestAliveDistance=None,
                        initialLegacyShots=actor['legacyCumulativeShots'], maximumLegacyShots=actor['legacyCumulativeShots'],
                        lastLegacyShots=actor['legacyCumulativeShots'], legacyCounterDecreases=0)
                out = actors[key]
                out['samples'] += 1
                out['kindEligibleSamples'] += actor['kindEligibleInRange'] > 0
                out['permittedEligibleSamples'] += actor['kindEligibleInRange'] > 0 and actor['operational'] and actor['combatPermitted']
                out['airInRangeSamples'] += actor['airInRange'] > 0
                out['groundInRangeSamples'] += actor['groundInRange'] > 0
                distance = actor['nearestAliveDistance']
                if distance is not None:
                    out['minimumNearestAliveDistance'] = distance if out['minimumNearestAliveDistance'] is None else min(distance, out['minimumNearestAliveDistance'])
                shots = actor['legacyCumulativeShots']
                if shots is not None:
                    out['legacyCounterDecreases'] += shots < out['lastLegacyShots']
                    out['maximumLegacyShots'] = max(shots, out['maximumLegacyShots'])
                    out['lastLegacyShots'] = shots
    assert last is not None, 'No coverage sample'
    report = dict(scope='Fixed prefix; opportunities sampled at one-second real-time intervals, not continuous proof.',
        sourceBytesAtRead=size, prefixBytes=consumed, prefixSha256=digest.hexdigest(), rows=sequence,
        firstGameSeconds=first['gameSeconds'], lastGameSeconds=last['gameSeconds'], day=last['day'], phase=last['phase'],
        samplesByDay=dict(days), actors=actors,
        limitations=['Legacy initial counters are cumulative since construction; prior individual shot times are unavailable.',
            'A legacy counter decrease can indicate normal combat clear/reset; maximum does not sum multiple reset segments.',
            'Range counts omit turn alignment and legacy Frost slow-status filtering; no per-weapon damage attribution.',
            'Main recorder BuildingInstance signals do not observe legacy attacks.'])
    path = root / 'checkpoints' / f'combat-coverage-prefix-{sequence:09d}.json'
    path.parent.mkdir(exist_ok=True)
    with path.open('x') as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps(report, ensure_ascii=False, indent=2))
    print('Saved:', path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    summarize(parser.parse_args().run)
