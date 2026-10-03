"""Losslessly archive closed observer logs; preserve original hashes before removing raw copies."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path


def digest_stream(stream):
    digest, size = hashlib.sha256(), 0
    for block in iter(lambda: stream.read(1024*1024), b''):
        digest.update(block)
        size += len(block)
    return digest.hexdigest(), size


def archive(root):
    root = root.resolve()
    project = Path(__file__).resolve().parents[3]
    if not root.is_relative_to(project / 'Docs/Measurements/Balance'):
        raise ValueError('Expected project recording directory')
    status = json.loads((root/'status.json').read_text())
    audit = json.loads((root/'audit.json').read_text())
    if not status['stopped'] or status['error'] or not audit['passed']:
        raise ValueError('Clean stop and successful terminal audit required')
    names = ['events']
    if (root/'combat-coverage.jsonl').exists():
        coverage = json.loads((root/'combat-coverage-status.json').read_text())
        coverage_audit = json.loads((root/'combat-coverage-audit.json').read_text())
        if not coverage['stopped'] or coverage['error'] or not coverage_audit['passed']:
            raise ValueError('Clean supplemental stop required')
        names.append('combat-coverage')
    for name in names:
        raw, target, manifest = root/(name+'.jsonl'), root/(name+'.jsonl.gz'), root/(name+'-archive.json')
        if not raw.exists() or target.exists() or manifest.exists():
            raise ValueError('Expected new archive and existing raw source: '+name)
        temp = target.with_suffix(target.suffix+'.tmp')
        with raw.open('rb') as source, temp.open('xb') as output:
            with gzip.GzipFile(filename='',mode='wb',fileobj=output,mtime=0) as compressed:
                for block in iter(lambda: source.read(1024*1024), b''):
                    compressed.write(block)
        with raw.open('rb') as source:
            before = digest_stream(source)
        with gzip.open(temp,'rb') as restored:
            after = digest_stream(restored)
        if before != after:
            raise ValueError('Archive round-trip mismatch; raw preserved: '+name)
        temp.rename(target)
        with target.open('rb') as compressed:
            archived = digest_stream(compressed)
        evidence = dict(file=raw.name, rawSha256=before[0], rawBytes=before[1],
            archive=target.name, archiveSha256=archived[0], archiveBytes=archived[1],
            roundTripVerified=True, rawRemovedAfterVerification=False)
        with manifest.open('x') as stream:
            json.dump(evidence,stream,indent=2);stream.write('\n')
        raw.unlink()
        evidence['rawRemovedAfterVerification'] = True
        manifest.write_text(json.dumps(evidence, indent=2)+'\n')
        print(json.dumps(evidence))


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('run',type=Path)
    archive(p.parse_args().run)
