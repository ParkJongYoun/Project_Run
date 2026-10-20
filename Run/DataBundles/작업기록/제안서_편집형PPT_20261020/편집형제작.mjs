import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Presentation, PresentationFile } from "file:///C:/Users/ParkJongYoun/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs";
process.on("uncaughtException", (error) => { console.error(error.message); process.exit(1); });

const work = "D:/Project_Run/Run/DataBundles/작업기록/제안서_편집형PPT_20261020";
const pages = JSON.parse(await fs.readFile(path.join(work, "원본요소.json"), "utf8"));
const scale = 4 / 3;
// The embedded PDF font has a 0.1-em baseline offset relative to the
// installed Malgun Gothic font that PowerPoint uses for editable text.
const ascent = 0.9888671875;
const presentation = Presentation.create({ slideSize: { width: 1280, height: 720 } });
presentation.theme.defaultFont = "맑은 고딕";
const none = { fill: "none", width: 0 };

function addRect(slide, rect) {
  slide.shapes.add({
    geometry: "rect",
    name: "원본 도형",
    position: { left: rect.x * scale, top: rect.y * scale, width: rect.w * scale, height: rect.h * scale },
    fill: rect.fill || "none",
    line: rect.stroke ? { fill: rect.stroke, width: Math.max(rect.lineWidth * scale, 0.25), style: "solid" } : none,
  });
}

function nativeRuns(runs) {
  return runs.map(({ text, style }) => ({
    run: text,
    style: {
      typeface: "맑은 고딕",
      fontSize: `${style.size}pt`,
      bold: style.bold,
      color: style.color,
    },
  }));
}

function addText(slide, line) {
  const left = line.x * scale;
  const size = line.size * scale;
  const shape = slide.shapes.add({
    geometry: "textbox",
    name: line.text,
    position: {
      left,
      top: (line.baseline - line.size * ascent) * scale,
      width: Math.min(1280 - left, (line.right - line.x) * scale + size * 0.5),
      height: Math.min(size * 1.5, 720 - (line.baseline - line.size * ascent) * scale),
    },
    fill: "none",
    line: none,
  });
  shape.text = [nativeRuns(line.runs)];
  shape.text.style = {
    typeface: "맑은 고딕",
    fontSize: size,
    color: line.runs[0].style.color,
    alignment: "left",
    verticalAlignment: "top",
    insets: 0,
    wrap: "none",
    autoFit: "none",
  };
}

function addTable(slide, data) {
  const { xs, ys, cells } = data;
  const table = slide.tables.add({
    rows: cells.length,
    columns: cells[0].length,
    left: xs[0] * scale,
    top: ys[0] * scale,
    width: (xs.at(-1) - xs[0]) * scale,
    height: (ys.at(-1) - ys[0]) * scale,
    columnWidths: xs.slice(1).map((x, index) => (x - xs[index]) * scale),
    values: cells.map(row => row.map(cell => [nativeRuns(cell.runs)])),
  });
  table.styleOptions = { headerRow: false, bandedRows: false, firstColumn: false, lastColumn: false };
  table.borders.assign({ fill: "none", width: 0 });
  for (let row = 0; row < cells.length; row++) {
    table.rows[row].height = (ys[row + 1] - ys[row]) * scale;
    for (let column = 0; column < cells[row].length; column++) {
      const cellData = cells[row][column];
      const cell = table.getCell(row, column);
      cell.fill = cellData.fill;
      cell.text.style = {
        typeface: "맑은 고딕",
        fontSize: cellData.size * scale,
        color: cellData.runs[0].style.color,
        bold: cellData.runs[0].style.bold,
        wrap: "none",
        autoFit: "none",
      };
      table.cells.block({ row, column, rowCount: 1, columnCount: 1 }).assign({
        fill: cellData.fill,
        margins: {
          left: (cellData.text_x - xs[column]) * scale,
          right: 0,
          top: Math.max(0, (cellData.baseline - ys[row] - cellData.size * ascent) * scale),
          bottom: 0,
        },
        anchor: "top",
      });
    }
  }
}

for (const page of pages) {
  const slide = presentation.slides.add();
  slide.background.fill = "#FFFFFF";
  for (const rect of page.rectangles) addRect(slide, rect);
  for (const line of page.lines) {
    slide.shapes.add({
      geometry: "line", name: "원본 선",
      position: { left: line.x * scale, top: line.y * scale, width: line.w * scale, height: line.h * scale },
      fill: "none", line: { fill: line.color, width: line.lineWidth * scale },
    });
  }
  for (const sourceImage of page.images) {
    const bytes = await fs.readFile(sourceImage.path);
    slide.images.add({
      blob: bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
      contentType: "image/png",
      alt: `원본 ${page.number}쪽 그림`,
      position: {
        left: sourceImage.x * scale, top: sourceImage.y * scale,
        width: sourceImage.w * scale, height: sourceImage.h * scale,
      },
    });
  }
  for (const line of page.text) addText(slide, line);
  if (page.table) addTable(slide, page.table);
}

const candidatePath = path.join(work, "편집형초안.pptx");
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);
await fs.mkdir(path.join(work, "초안검수"), { recursive: true });
for (const index of [0, 1, 6, 10]) {
  const blob = await presentation.export({ slide: presentation.slides.items[index], format: "png", scale: 1 });
  await fs.writeFile(path.join(work, "초안검수", `${index + 1}.png`), new Uint8Array(await blob.arrayBuffer()));
}
console.log(JSON.stringify({ candidatePath, slides: presentation.slides.items.length }));
