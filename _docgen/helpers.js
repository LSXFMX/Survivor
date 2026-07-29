const {
  Paragraph, TextRun, Table, TableRow, TableCell, ImageRun,
  AlignmentType, LevelFormat, HeadingLevel, BorderStyle, WidthType,
  ShadingType, VerticalAlign, PageBreak, TableOfContents,
} = require("docx");
const fs = require("fs");
const path = require("path");

// 配色方案：标题统一黑色，仅用一个暗红作为强调色，表头用深灰底
const COLOR = {
  black: "000000",
  accent: "8C1D18",     // 强调色（暗红），仅用于需要着重标记的文字
  gray: "595959",
  headerFill: "3B3B3B", // 表头底色（深灰）
  zebraFill: "F2F2F2",
  white: "FFFFFF",
};

function H1(text) {
  return new Paragraph({
    heading: HeadingLevel.HEADING_1,
    children: [new TextRun(text)],
    pageBreakBefore: true,
  });
}
function H2(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(text)] });
}
function H3(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(text)] });
}
function H4(text) {
  return new Paragraph({ heading: HeadingLevel.HEADING_4, children: [new TextRun(text)] });
}

function P(text, opts = {}) {
  const { bold, italic, color, size, alignment, spacingAfter = 120 } = opts;
  return new Paragraph({
    alignment,
    spacing: { after: spacingAfter },
    children: [new TextRun({ text, bold, italics: italic, color, size })],
  });
}

// 多个 run 拼接的段落（用于部分加粗/变色文字混排）
function PMix(runs, opts = {}) {
  return new Paragraph({
    alignment: opts.alignment,
    spacing: { after: opts.spacingAfter ?? 120 },
    children: runs.map((r) => new TextRun(r)),
  });
}

function Bullet(text, opts = {}) {
  return new Paragraph({
    numbering: { reference: "bullets", level: opts.level || 0 },
    spacing: { after: 60 },
    children: [new TextRun({ text, color: opts.color, bold: opts.bold })],
  });
}

function Num(text, opts = {}) {
  return new Paragraph({
    numbering: { reference: "numbers", level: opts.level || 0 },
    spacing: { after: 60 },
    children: [new TextRun({ text, color: opts.color, bold: opts.bold })],
  });
}

function Quote(text) {
  return new Paragraph({
    indent: { left: 480 },
    border: { left: { style: BorderStyle.SINGLE, size: 12, color: "BFBFBF", space: 8 } },
    spacing: { after: 160, before: 80 },
    children: [new TextRun({ text, italics: true, color: COLOR.gray })],
  });
}

function Divider() {
  return new Paragraph({
    border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: "BFBFBF", space: 1 } },
    spacing: { after: 200 },
    children: [],
  });
}

// ---- 表格构建 ----
function MakeTable(header, rows, widths, opts = {}) {
  const tableWidth = widths.reduce((a, b) => a + b, 0);
  const border = { style: BorderStyle.SINGLE, size: 1, color: "BFBFBF" };
  const borders = { top: border, bottom: border, left: border, right: border };
  const headerFill = opts.headerFill || COLOR.headerFill;
  const zebraFill = opts.zebraFill || COLOR.zebraFill;

  function cell(text, i, isHeader, rowIdx) {
    return new TableCell({
      borders,
      width: { size: widths[i], type: WidthType.DXA },
      shading: {
        fill: isHeader ? headerFill : rowIdx % 2 === 1 ? zebraFill : "FFFFFF",
        type: ShadingType.CLEAR,
      },
      verticalAlign: VerticalAlign.CENTER,
      margins: { top: 60, bottom: 60, left: 100, right: 100 },
      children: [
        new Paragraph({
          alignment: opts.align || AlignmentType.LEFT,
          children: [
            new TextRun({
              text: String(text ?? ""),
              bold: isHeader,
              color: isHeader ? "FFFFFF" : "262626",
              size: opts.fontSize || 19,
            }),
          ],
        }),
      ],
    });
  }

  const headerRow = new TableRow({
    tableHeader: true,
    children: header.map((h, i) => cell(h, i, true, 0)),
  });
  const dataRows = rows.map(
    (r, ridx) => new TableRow({ children: r.map((c, i) => cell(c, i, false, ridx + 1)) })
  );
  return new Table({
    width: { size: tableWidth, type: WidthType.DXA },
    columnWidths: widths,
    rows: [headerRow, ...dataRows],
  });
}

// ---- 图片 / 占位符 ----
function imgBuf(relPath) {
  const p = path.join(__dirname, "imgs", relPath);
  return fs.readFileSync(p);
}

function ImageCentered(relPath, w, h, altTitle) {
  return new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 120, before: 80 },
    children: [
      new ImageRun({
        type: "png",
        data: imgBuf(relPath),
        transformation: { width: w, height: h },
        altText: { title: altTitle || relPath, description: altTitle || relPath, name: relPath },
      }),
    ],
  });
}

function Caption(text) {
  return new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { after: 200 },
    children: [new TextRun({ text, italics: true, color: COLOR.gray, size: 18 })],
  });
}

// 一行并排两张图（横向对比展示），各自附小标题
function ImageRow2(relPath1, cap1, relPath2, cap2, w, h) {
  const border = { style: BorderStyle.SINGLE, size: 1, color: "BFBFBF" };
  const borders = { top: border, bottom: border, left: border, right: border };
  const cellW = 4640;
  function cell(relPath, cap) {
    return new TableCell({
      borders,
      width: { size: cellW, type: WidthType.DXA },
      margins: { top: 100, bottom: 100, left: 100, right: 100 },
      children: [
        new Paragraph({
          alignment: AlignmentType.CENTER,
          children: [
            new ImageRun({
              type: "png",
              data: imgBuf(relPath),
              transformation: { width: w, height: h },
              altText: { title: cap, description: cap, name: relPath },
            }),
          ],
        }),
        new Paragraph({
          alignment: AlignmentType.CENTER,
          spacing: { before: 60 },
          children: [new TextRun({ text: cap, italics: true, color: COLOR.gray, size: 18 })],
        }),
      ],
    });
  }
  return new Table({
    width: { size: cellW * 2, type: WidthType.DXA },
    columnWidths: [cellW, cellW],
    rows: [new TableRow({ children: [cell(relPath1, cap1), cell(relPath2, cap2)] })],
  });
}

// 图片占位符：虚线框 + 说明文字，供手动替换截图
function ImagePlaceholder(caption, sizeHint = "建议尺寸1280x720") {
  return [
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 120, after: 40 },
      border: {
        top: { style: BorderStyle.DASHED, size: 8, color: "A6A6A6", space: 8 },
        bottom: { style: BorderStyle.DASHED, size: 8, color: "A6A6A6", space: 8 },
        left: { style: BorderStyle.DASHED, size: 8, color: "A6A6A6", space: 8 },
        right: { style: BorderStyle.DASHED, size: 8, color: "A6A6A6", space: 8 },
      },
      children: [
        new TextRun({ text: " ", size: 20 }),
        new TextRun({ text: " ", size: 20 }),
        new TextRun({ text: " ", size: 20 }),
      ],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 40 },
      children: [
        new TextRun({ text: "截图占位　", bold: true, color: COLOR.black, size: 22 }),
        new TextRun({ text: caption, color: COLOR.black, size: 22 }),
      ],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { after: 200 },
      children: [new TextRun({ text: sizeHint, italics: true, color: "808080", size: 18 })],
    }),
  ];
}

module.exports = {
  COLOR, H1, H2, H3, H4, P, PMix, Bullet, Num, Quote, Divider,
  MakeTable, ImageCentered, ImageRow2, Caption, ImagePlaceholder, imgBuf,
};
