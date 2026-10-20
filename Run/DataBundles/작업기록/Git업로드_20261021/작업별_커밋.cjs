const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../../../..');
const out = __dirname;
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'project-run-git-'));
function git(args, options = {}) {
    const result = spawnSync('git', ['-c', 'core.quotepath=false', '-c', 'core.safecrlf=false', ...args],
        { cwd: root, maxBuffer: 64 * 1024 * 1024, ...options });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(args.join(' ') + '\n' + result.stderr?.toString('utf8') + result.stdout?.toString('utf8'));
    return result.stdout;
}
function paths(args) { return git(args).toString('utf8').split('\0').filter(Boolean); }
function hashFile(p) {
    const hash = crypto.createHash('sha256');
    const fd = fs.openSync(path.join(root, p), 'r');
    const buf = Buffer.alloc(4 * 1024 * 1024);
    try { for (let n; (n = fs.readSync(fd, buf)) > 0;) hash.update(buf.subarray(0, n)); }
    finally { fs.closeSync(fd); }
    return hash.digest('hex');
}
const recordPrefix = 'Run/DataBundles/작업기록/Git업로드_20261021/';
const demoRace = 'Run/Assets/Scripts/Game/Race/Presentation/Sys_DemoRace.cs';
const webBuild = 'Run/Assets/Scripts/Editor/Demo/DemoWebBuild.cs';
const settings = 'Run/ProjectSettings/ProjectSettings.asset';
const messages = [
    ['타이틀 원화를 교체하고 캐릭터 움직임을 연결', '몬스터 레이스 타이틀 이미지와 화면 연결 갱신', '원화와 정점 연출 및 제작 기록 보존'],
    ['목장 화면 배치와 길 표현을 보정하고 무음 적용', '몬스터 카드와 출전 선택 화면 및 얼굴 이미지 보정', '흙길 재질과 공통 소리 동작 및 확인 자료 갱신'],
    ['경기 여섯 몬스터의 전용 스킬 이펙트 연결', '몬스터별 이펙트 리소스와 독립 표시 모듈 추가', '영상 복귀와 기존 효과 중복 방지 및 검증 기록 반영'],
    ['경기 스킬 발동을 분산하고 추월 동작 개선', '스킬 횟수와 발동 시점 및 경기 테이블 갱신', '주행 경로와 후반 추월 및 반복 검증 자료 반영'],
    ['필살기 영상의 건너뛰기와 일시정지 버튼 제거', '경기 영상 패널과 생성 도구의 버튼 연결 정리', '영상 제어 변경과 관련 확인 기록 보존'],
    ['아이콘이 포함된 플레이 실행기를 웹 빌드에 자동 생성', '로컬 서버를 포함한 Play 실행기와 빌드 후처리 추가', '승인 아이콘 원본과 실행기 검증 기록 보존'],
    ['화질을 유지하며 웹 빌드 용량을 줄이고 산출물 갱신', 'Gzip과 코드 크기 최적화 및 미사용 리소스 제외 적용', '완성 빌드와 이전 백업 및 용량 검증 자료 보존'],
    ['몬스터 레이스 제안서와 편집형 발표 자료 추가', '제안서 PPT와 편집용 제작 자료 보존', '변환 도구와 화면 확인 기록 추가'],
    ['Unity 클라우드 프로젝트 연결 정보를 갱신', '프로젝트 이름과 조직 연결 정보 반영', '현재 Run 프로젝트 식별자 저장'],
    ['실행기와 용량 최적화 및 최신 게임 개발 기준 갱신', '목장과 경기 스킬 및 영상 조작 기준 반영', '자동 실행기와 웹 빌드 용량 관리 규칙 명시'],
];
function classify(p) {
    if (p.startsWith(recordPrefix)) return 11;
    if (p === 'AGENTS.md' || p === 'Run/DataBundles/기획/Project_Run_개발_규칙.md') return 10;
    if (/제안서/.test(p)) return 8;
    if (/메인타이틀_|title_race_background|UIItem_TitleArtworkMotion|UIPanel_DemoTitle|Data_DemoArt\.asset$/.test(p)) return 1;
    if (/목장UI|\/ArtBundles\/Ranch\/|\/ArtBundles\/UI\/Ranch\/|\/ArtBundles\/UI\/RanchCollection\/|\/Editor\/Ranch\/|\/UIPanel_Ranch|\/Mgr_Game\.cs$|\/Mgr_Sound\.cs$|\/AudioManager\.asset$/.test(p)) return 2;
    if (/\/ArtBundles\/Race(?:\/|\.meta$)|\/Resources\/RaceSkillEffects(?:\/|\.meta$)|\/(?:Data|Sys)_RaceSkillEffects\.cs|\/RaceSkillEffects(?:Builder|Validation)\.cs|\/Sys_RaceCinemachine\.cs$|\/Sys_DemoSkillEffects\.cs$|\/RaceRunnerView\.cs$|경기6종_스킬이펙트_|\/import_effects\.py$/.test(p)) return 3;
    if (/Table_RaceSkills|경기스킬_6종|경기스킬분산과_|\/RaceTacticsValidation\.cs|\/RaceBroadcast(?:Playthrough|Validation)\.cs|\/Data_Race(?:SkillSchedule|LaneContest|Simulation)\.cs|\/Sys_Race\.cs$|\/UIPanel_DemoRace\.Broadcast\.cs$|\/update_tactics_table\.mjs$/.test(p) || p === demoRace) return 4;
    if (/필살기영상_버튼제거_|\/UIPanel_DemoRace(?:\.prefab|\.Video\.cs|\.cs)$|\/DemoRacePrefabBuilder\.cs$|\/DemoSkillVideoValidation\.cs$/.test(p)) return 5;
    if (/실행아이콘|플레이실행기_|\/WebLauncherPostprocessor\.cs|\/(?:BuildLauncher\.ps1|PlayLauncher\.cs|TestLauncher\.ps1)$/.test(p)) return 6;
    if (p === '.gitattributes' || p === settings || p === webBuild || /\/빌드\/|\/DemoWebBuildResources\.cs|\/Fonts\/SDF\//.test(p)) return 7;
    throw new Error('Unclassified path: ' + p);
}
function replaceExactly(text, before, after) {
    if (text.split(before).length !== 2) throw new Error('Partial staging match is not unique: ' + before);
    return text.replace(before, after);
}
function partialContent(group, p, original) {
    let head = git(['show', 'HEAD:' + p]).toString('utf8');
    if (group === 3 && p === demoRace) {
        const anchor = '            gameObject.AddComponent<Sys_DemoSkillEffects>();';
        const addition = '\n            // Optional six-runner purchased FX; removing this hook restores the legacy presentation.\n            if (gameObject.scene.name == Define_Scene.OvalToon)\n                gameObject.AddComponent<Sys_RaceSkillEffects>();';
        return replaceExactly(head, anchor, anchor + addition);
    }
    if (group === 6 && p === webBuild) {
        const start = '                File.Copy("../Tools/LocalWeb/Serve.ps1",output + "/Serve.ps1",true);';
        const end = '                File.Copy("Tools/Demo/" + launcher,output + "/" + launcher,true);';
        const a = head.indexOf(start), b = head.indexOf(end);
        if (a < 0 || b < a) throw new Error('Legacy launcher block was not found');
        head = head.slice(0, a) + '                if (!File.Exists(output + "/Play.exe")) throw new InvalidOperationException("Play.exe was not packaged.");' + head.slice(b + end.length);
        head = replaceExactly(head, '실행: Play.cmd', '실행: Play.exe');
        return head;
    }
    if (group === 7 && p === settings) {
        return replaceExactly(head, '  webGLCompressionFormat: 2', '  webGLCompressionFormat: 1');
    }
    throw new Error('Unknown partial stage');
}
const mode = process.argv[2] || 'plan';
if (mode === 'plan') {
    if (paths(['diff', '--cached', '--name-only', '-z']).length) throw new Error('Index must be empty before planning');
    const files = [...new Set([...paths(['diff', '--name-only', '-z']), ...paths(['ls-files', '--others', '--exclude-standard', '-z'])])];
    const inventory = files.map(p => ({ path: p, group: classify(p), bytes: fs.statSync(path.join(root, p)).size, sha256: hashFile(p) }));
    const secretPatterns = [
        /(?:ghp_|github_pat_)[A-Za-z0-9_]{30,}/g,
        /AKIA[A-Z0-9]{16}/g,
        /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/g,
        /\bsk-(?:proj-)?[A-Za-z0-9_-]{40,}/g,
    ];
    const findings = [];
    for (const f of inventory) {
        if (f.bytes > 8 * 1024 * 1024 || !/\.(?:cs|ps1|py|mjs|cjs|js|json|txt|md|html|yaml|yml|env)$/i.test(f.path) || f.path.startsWith(recordPrefix)) continue;
        const data = fs.readFileSync(path.join(root, f.path), 'utf8');
        secretPatterns.forEach((pattern, i) => { pattern.lastIndex = 0; if (pattern.test(data)) findings.push({ path: f.path, pattern: i }); });
    }
    const large = inventory.filter(f => f.bytes > 50 * 1024 * 1024).map(f => ({
        path: f.path, bytes: f.bytes, attr: git(['check-attr', 'filter', '--', f.path]).toString('utf8').trim(),
    }));
    if (findings.length) throw new Error('Credential candidates need review: ' + JSON.stringify(findings));
    if (large.some(f => !f.attr.endsWith(': lfs'))) throw new Error('Large file is not in LFS: ' + JSON.stringify(large));
    fs.writeFileSync(path.join(out, '변경파일_분류.json'), JSON.stringify(inventory, null, 2) + '\n');
    fs.writeFileSync(path.join(out, '업로드전_확인.json'), JSON.stringify({
        branch: git(['branch', '--show-current']).toString().trim(),
        base: git(['rev-parse', 'HEAD']).toString().trim(),
        fileCount: inventory.length, totalBytes: inventory.reduce((sum, f) => sum + f.bytes, 0),
        credentialCandidates: findings, largeFiles: large,
    }, null, 2) + '\n');
    for (let i = 1; i <= 11; i++) {
        const group = inventory.filter(f => f.group === i);
        console.log(i, messages[i - 1]?.[0] || 'Git 업로드 기록', group.length, group.reduce((sum, f) => sum + f.bytes, 0));
    }
} else if (mode === 'commit') {
    const group = Number(process.argv[3]);
    if (!Number.isInteger(group) || group < 1 || group > 10) throw new Error('Invalid commit group');
    const inventory = JSON.parse(fs.readFileSync(path.join(out, '변경파일_분류.json'), 'utf8'));
    const selected = inventory.filter(f => f.group === group && f.path !== settings);
    if (group === 9) selected.push(inventory.find(f => f.path === settings));
    const partial = group === 3 ? [demoRace] : group === 6 ? [webBuild] : group === 7 ? [settings] : [];
    const allowed = new Set([...selected.map(f => f.path), ...partial]);
    if (paths(['diff', '--cached', '--name-only', '-z']).some(p => !allowed.has(p))) throw new Error('Index contains a different task');
    for (const f of [...selected, ...partial.map(p => inventory.find(f => f.path === p))]) {
        if (!f || hashFile(f.path) !== f.sha256) throw new Error('Working file changed since review: ' + f?.path);
    }
    const selectedPaths = selected.map(f => f.path);
    if (selectedPaths.length) {
        const pathspec = path.join(temp, 'paths');
        fs.writeFileSync(pathspec, selectedPaths.join('\0') + '\0');
        git(['add', '--pathspec-from-file=' + pathspec, '--pathspec-file-nul']);
    }
    for (const p of partial) {
        const content = partialContent(group, p);
        const blob = git(['hash-object', '-w', '--stdin'], { input: content }).toString().trim();
        git(['update-index', '--add', '--cacheinfo', '100644,' + blob + ',' + p]);
    }
    const staged = paths(['diff', '--cached', '--name-only', '-z']);
    if (staged.some(p => !allowed.has(p))) throw new Error('Unexpected path in index');
    // Keep copied third-party shader sources byte-for-byte, including their whitespace.
    git(['-c', 'core.whitespace=blank-at-eol,space-before-tab,-blank-at-eof', 'diff', '--cached', '--check', '--', '*.cs', '*.shader', '*.py', '*.ps1', '*.mjs', '*.cjs',
        ':(exclude)Run/Assets/ArtBundles/Race/SkillEffects/Shaders/**']);
    const [title, a, b] = messages[group - 1];
    const messagePath = path.join(temp, 'message.txt');
    fs.writeFileSync(messagePath, title + '\n\n- ' + a + '\n- ' + b + '\n');
    git(['commit', '-F', messagePath]);
    const sha = git(['rev-parse', 'HEAD']).toString().trim();
    fs.appendFileSync(path.join(out, '커밋결과.jsonl'), JSON.stringify({ group, commit: sha, title, files: staged.length }) + '\n');
    console.log(sha.slice(0, 10), title, staged.length + ' files');
} else if (mode === 'verify') {
    const inventory = JSON.parse(fs.readFileSync(path.join(out, '변경파일_분류.json'), 'utf8'));
    const changed = inventory.filter(f => !f.path.startsWith(recordPrefix) && hashFile(f.path) !== f.sha256).map(f => f.path);
    if (changed.length) throw new Error('Working file content changed: ' + JSON.stringify(changed));
    const remaining = [...paths(['diff', '--name-only', '-z']), ...paths(['ls-files', '--others', '--exclude-standard', '-z'])];
    if (remaining.some(p => !p.startsWith(recordPrefix))) throw new Error('Uncommitted work remains: ' + JSON.stringify(remaining));
    console.log('All original file bytes preserved; only this upload record remains.');
} else throw new Error('Unknown mode');
