import fs from "node:fs/promises";
import path from "node:path";
import { createHash } from "node:crypto";
import { pathToFileURL } from "node:url";
process.on("uncaughtException", (error) => { console.error(error.message); process.exit(1); });

const workspaceDir = "D:/Project_Run/Run/DataBundles";
const work = path.join(workspaceDir, "작업기록/제안서_편집형PPT_20261020");
const skill = "C:/Users/ParkJongYoun/.codex/plugins/cache/openai-primary-runtime/presentations/26.1017.31632/skills/presentations";
const source = "C:/Users/ParkJongYoun/Downloads/RO_몬스터_레이스_제안서_가을목장반영본.pdf";
const finalPath = path.join(workspaceDir, "기획/제안서_PPT변환/RO_몬스터_레이스_제안서_가을목장반영본_편집가능.pptx");
process.env.RUNTIME_NODE_MODULES = "C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const { finalizePresentation } = await import(pathToFileURL(path.join(skill, "container_tools/artifact_tool_utils.mjs")).href);

const originalLink = fs.link;
fs.link = async (from, to) => {
  try { return await originalLink(from, to); }
  catch (error) {
    if (error.code !== "EPERM" || path.resolve(to) !== path.resolve(finalPath) || !String(from).endsWith(".validated")) throw error;
    return fs.copyFile(from, to, fs.constants.COPYFILE_EXCL);
  }
};

const result = await finalizePresentation({
  workspaceDir,
  candidatePath: path.join(work, "편집형초안.pptx"),
  finalPath,
  pythonExecutable: "C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe",
  integrityValidatorPath: path.join(skill, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(skill, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: [
    "--expected-slide-size-emu", "12192000,6858000",
    "--require-native-table-slide", "7",
    "--require-native-table-slide", "11",
  ],
  explicitTotalSlideCount: 11,
  requiredNativeTableOwnerSlides: [7, 11],
  requiredNativeChartOwnerSlides: [],
  fontPolicy: {
    basis: "reference",
    families: ["맑은 고딕"],
    referencePath: source,
    referenceSha256: createHash("sha256").update(await fs.readFile(source)).digest("hex"),
  },
  verifyArtifactToolImport: true,
  receiptPath: path.join(work, "최종검증.json"),
});
console.log(JSON.stringify({
  finalPath: result.finalPath,
  byteCount: result.byteCount,
  integrity: result.packageIntegrity.status,
  layout: result.presentationLayout,
  fonts: result.fontSelection,
  import: result.firstPartyImport.passed,
}));
