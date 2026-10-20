import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { FileBlob, Presentation, PresentationFile } from "file:///C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs";

const workspaceDir = "D:/Project_Run/Run/DataBundles";
const buildDir = path.join(workspaceDir, "작업기록/제안서_PPT변환_20261020");
const finalPath = path.join(
  workspaceDir,
  "기획/제안서_PPT변환/RO_몬스터_레이스_제안서_가을목장반영본.pptx",
);
const skillDir = "C:/Users/ParkJongYoun/.codex/plugins/cache/openai-primary-runtime/presentations/26.1017.31632/skills/presentations";
const runtimePython = "C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe";
process.env.RUNTIME_NODE_MODULES = "C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const { finalizePresentation } = await import(
  pathToFileURL(path.join(skillDir, "container_tools/artifact_tool_utils.mjs")).href,
);
const pageDir = path.join(buildDir, "원본페이지");
const pages = (await fs.readdir(pageDir))
  .filter((name) => name.endsWith(".png"))
  .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
if (pages.length !== 11) throw new Error(`Expected 11 pages; found ${pages.length}`);

const presentation = Presentation.create({
  slideSize: { width: 1280, height: 720 },
});
for (const [index, page] of pages.entries()) {
  const bytes = await fs.readFile(path.join(pageDir, page));
  const slide = presentation.slides.add();
  slide.images.add({
    blob: bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    contentType: "image/png",
    alt: `원본 PDF ${index + 1}쪽`,
    fit: "contain",
    position: { left: 0, top: 0, width: 1280, height: 720 },
  });
}

const candidatePath = path.join(buildDir, "변환초안.pptx");
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);
console.log("Draft exported with 11 full-page images.");
const originalLink = fs.link;
fs.link = async (source, destination) => {
  try {
    return await originalLink(source, destination);
  } catch (error) {
    if (
      error.code !== "EPERM" ||
      path.resolve(destination) !== path.resolve(finalPath) ||
      !String(source).endsWith(".validated")
    ) throw error;
    // This Windows workspace disallows hard links. Publish the already
    // validated bytes with the same no-overwrite guarantee instead.
    return fs.copyFile(source, destination, fs.constants.COPYFILE_EXCL);
  }
};
const result = await finalizePresentation({
  workspaceDir,
  candidatePath,
  finalPath,
  pythonExecutable: runtimePython,
  integrityValidatorPath: path.join(skillDir, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(skillDir, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", "12192000,6858000"],
  explicitTotalSlideCount: 11,
  requiredNativeTableOwnerSlides: [],
  requiredNativeChartOwnerSlides: [],
  verifyArtifactToolImport: true,
  receiptPath: path.join(buildDir, "변환검증.json"),
});
console.log(JSON.stringify(result));

const finalPresentation = await PresentationFile.importPptx(await FileBlob.load(finalPath));
for (const [index, slide] of finalPresentation.slides.items.entries()) {
  const preview = await finalPresentation.export({ slide, format: "png", scale: 1 });
  await fs.writeFile(
    path.join(buildDir, "검증이미지", `슬라이드_${String(index + 1).padStart(2, "0")}.png`),
    new Uint8Array(await preview.arrayBuffer()),
  );
  console.log(`Rendered final slide ${index + 1}/11.`);
}
