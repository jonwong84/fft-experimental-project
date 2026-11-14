"""Independent ZOD-047 native-check, source-isolation and copied-metadata audit."""
import argparse
import base64
import copy
import hashlib
import html
import json
from pathlib import Path
from PIL import Image

CAPTURES = ('source', 'copy', 'invalid', 'preview', 'reopened', 'export-error',
            'unsaved-cancel', 'resized', 'unsaved-discard')
ACTIONS = ('empty search', 'search restored', 'copy', 'field-specific Move error',
           'valid Move', 'exact authored range', 'export', 'reopen',
           'failed export preserves output and draft', 'close canceled',
           'source preserved', 'finite time budget', 'discard closes editor')
WORKSPACE_CAPTURES = ('source', 'source-abilities', 'create-copy', 'copy', 'invalid', 'ability-error',
                     'abilities', 'reference', 'preview', 'reopened', 'package-details', 'export-error',
                     'unsaved-cancel', 'resized', 'resized-abilities', 'unsaved-discard')
WORKSPACE_ACTIONS = ('source job read-only', 'source ability catalog read-only', 'empty search', 'search restored',
    'copy', 'field-specific Move error', 'separate workspaces', 'ability error routes independently',
    'independent filters preserve job edits', 'ability filter preserves JP edit', 'original view remains unchanged',
    'view switch retains draft', 'job reference navigates to ability', 'valid Move', 'exact authored range',
    'export', 'reopen', 'failed export preserves output and draft', 'close canceled', 'source preserved',
    'finite time budget', 'discard closes editor')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_copy(source, authored):
    require(source['kind'] == 'baseline' and authored['kind'] == 'addon', 'Wrong package roles')
    require(authored['profileId'] == source['profileId'] and
            authored['requiredBaselinePackageId'] == source['packageId'], 'Wrong baseline/profile binding')
    require(len(authored['jobs']) == len(authored['skillsets']) == 1, 'Expected one copied job/skillset')
    owner = authored['packageId'].split(':')[0]
    require(owner == 'editor.demo' and owner != source['packageId'].split(':')[0], 'Wrong authored namespace')
    remap = lambda value: owner + ':' + value.split(':', 1)[1]
    job = next(job for job in source['jobs'] if remap(job['id']) == authored['jobs'][0]['id'])
    skillset = next(item for item in source['skillsets'] if item['id'] == job['primarySkillsetId'])
    referenced = set(job['innateAbilityIds']) | {slot['abilityId'] for slot in skillset['slots'] if slot['abilityId'] is not None}
    expected = copy.deepcopy(source)
    expected.update(packageId=authored['packageId'], kind='addon', requiredBaselinePackageId=source['packageId'])
    expected['jobs'] = [copy.deepcopy(job)]
    expected['jobs'][0].update(id=remap(job['id']), primarySkillsetId=remap(job['primarySkillsetId']),
                             baseMove=2, baseJump=4, innateAbilityIds=list(map(remap, job['innateAbilityIds'])))
    expected['skillsets'] = [copy.deepcopy(skillset)]
    expected['skillsets'][0]['id'] = remap(skillset['id'])
    for slot in expected['skillsets'][0]['slots']:
        if slot['abilityId'] is not None:
            slot['abilityId'] = remap(slot['abilityId'])
    expected['abilities'] = [copy.deepcopy(item) for item in source['abilities'] if item['id'] in referenced]
    expected['abilities'].sort(key=lambda item: remap(item['id']))
    for index, ability in enumerate(expected['abilities']):
        ability['id'] = remap(ability['id'])
        if index == 0:
            ability['jpCost'] = 123
    require(authored == expected, 'Copy changed unsupported metadata, ordered references or edited values')
    return job


def verify(source_path, first, repeat):
    source_bytes = source_path.read_bytes()
    source = json.loads(source_bytes)
    digest = hashlib.sha256(source_bytes).hexdigest()
    inventories = []
    for directory in (first, repeat):
        files = {path.name: path.read_bytes() for path in directory.iterdir() if path.is_file()}
        trace = json.loads(files['trace.json'])
        require(trace['format'] == 'fft.job-editor-native-check' and trace['schemaVersion'] in (1, 2), 'Wrong report')
        captures = WORKSPACE_CAPTURES if trace['schemaVersion'] == 2 else CAPTURES
        require(set(files) == {name + '.png' for name in captures} | {'trace.json', 'authored-job.json'}, 'Unexpected/missing native output')
        validate_route(trace)
        require(trace['sourceSha256'] == digest and trace['sourcePackage'] == source['packageId'], 'Source identity changed')
        job = validate_copy(source, json.loads(files['authored-job.json']))
        require(trace['sourceMove'] == job['baseMove'] and trace['authoredMove'] == 1 and
                (trace['savedMove'], trace['savedJump'], trace['savedJp']) == (2, 4, 123), 'Wrong draft/export separation')
        require(all(trace[key] is True for key in ('sourceUnchanged', 'exportedRoundTrip', 'failedExportPreserved',
                    'closeCancelPreserved', 'closeDiscardAccepted')), 'Missing preservation/close check')
        require(trace['captureKind'] == 'native DrawToBitmap; not desktop scanout or physical input', 'Unsupported presentation claim')
        for name in captures:
            with Image.open(directory / (name + '.png')) as image:
                image.load()
                require(image.width >= (400 if name.startswith('unsaved') or name in ('create-copy', 'package-details') else 1000) and image.height >= 150,
                        'Unexpected native image size')
        inventories.append(files)
    require(inventories[0] == inventories[1], 'Repeated native outputs differ')
    return dict(format='fft.job-editor-audit', schemaVersion=1, passed=True,
                sourceSha256=digest, exactRepeatedFiles=len(inventories[0]), nativeCapturesPerRun=len(captures),
                separateWorkspaces=trace['schemaVersion'] == 2,
                sourceMetadataPreserved=True, orderedReferenceClosurePreserved=True,
                savedMove=2, savedJump=4, savedFirstAbilityJp=123, unsavedDraftMove=1,
                physicalInputVerified=False, originalGameplayEquivalence=False)


def validate_route(trace):
    workspace = trace['schemaVersion'] == 2
    require(trace['steps'] == [dict(action=name, passed=True) for name in (WORKSPACE_ACTIONS if workspace else ACTIONS)],
            'Incomplete/reordered editor route')
    if workspace:
        require(all(trace[key] is True for key in ('separateWorkspaces', 'sourceFieldsReadOnly',
                    'independentFiltersPreservedEdits', 'jobReferenceNavigatesToAbility', 'validationSelectsWorkspace',
                    'viewSwitchPreservesDraft')), 'Missing workspace preservation/navigation check')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--first', type=Path, required=True)
    parser.add_argument('--repeat', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--review', type=Path, required=True)
    args = parser.parse_args()
    require(not args.output.exists() and not args.review.exists(), 'Refuse existing audit/review output')
    report = verify(args.source, args.first, args.repeat)
    cards = []
    captures = WORKSPACE_CAPTURES if report['separateWorkspaces'] else CAPTURES
    for name in captures:
        encoded = base64.b64encode((args.first / (name + '.png')).read_bytes()).decode('ascii')
        cards.append(f'<section><h2>{html.escape(name.replace("-", " "))}</h2><img src="data:image/png;base64,{encoded}" alt="{name}"></section>')
    page = '<!doctype html><meta charset="utf-8"><title>ZOD-047 editor review</title><style>body{font:16px system-ui;background:#edf0f5;color:#182535;max-width:1240px;margin:30px auto;padding:20px}section{background:white;padding:18px;margin:24px 0;border-radius:10px}img{max-width:100%;height:auto}h2{text-transform:capitalize}</style><h1>Jobs & Abilities workspaces • ZOD-047</h1><p>Native control renderings from the programmed check. Review independent searchable Jobs/Abilities pages, source/copy switching, related-record navigation, field-error routing, movement preview and shared package actions. Physical keyboard/mouse, file dialogs and owner approval remain separate.</p>' + ''.join(cards)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.review.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    args.review.write_text(page, encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
