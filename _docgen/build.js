const {
  Document, Packer, Paragraph, TextRun, AlignmentType, LevelFormat,
  HeadingLevel, PageBreak, TableOfContents, BorderStyle,
} = require("docx");
const fs = require("fs");
const path = require("path");
const { COLOR, P, PMix } = require("./helpers");

const ch01 = require("./ch01_overview");
const ch02 = require("./ch02_character");
const ch03 = require("./ch03_playable");
const ch04 = require("./ch04_scene");
const ch05 = require("./ch05_gate");
const ch06 = require("./ch06_boss");
const ch07 = require("./ch07_worldboss");
const ch08 = require("./ch08_equipsys");
const ch09 = require("./ch09_clearequip");
const ch10 = require("./ch10_achievement");
const ch11 = require("./ch11_favor");
const ch12 = require("./ch12_gacha");
const ch13 = require("./ch13_inherit");
const ch14 = require("./ch14_adventure");
const ch15 = require("./ch15_ui");
const ch16 = require("./ch16_appendix");

// ---- 封面 ----
function coverPage() {
  return [
    new Paragraph({ spacing: { before: 2400 }, children: [] }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      children: [new TextRun({ text: "幸存者", bold: true, size: 72, color: COLOR.black })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 200, after: 100 },
      children: [new TextRun({ text: "Survivor", bold: true, size: 40, color: COLOR.gray, italics: true })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 400 },
      children: [new TextRun({ text: "游戏策划案", size: 32, color: COLOR.accent, bold: true })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 100 },
      children: [new TextRun({ text: "像素风格自动战斗生存游戏", size: 24, color: COLOR.gray })],
    }),
    new Paragraph({ spacing: { before: 1600 }, children: [] }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      children: [new TextRun({ text: "个人独立游戏项目　完整设计文档", size: 20, color: COLOR.gray })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER,
      spacing: { before: 60 },
      children: [new TextRun({ text: "2026年8月", size: 20, color: COLOR.gray })],
    }),
    new Paragraph({ children: [new PageBreak()] }),
  ];
}

function tocPage() {
  return [
    new Paragraph({
      heading: HeadingLevel.HEADING_1,
      children: [new TextRun("目　录")],
    }),
    new TableOfContents("目录", { hyperlink: true, headingStyleRange: "1-3" }),
    new Paragraph({ children: [new PageBreak()] }),
  ];
}

const children = [
  ...coverPage(),
  ...tocPage(),
  ...ch01(),
  ...ch02(),
  ...ch03(),
  ...ch04(),
  ...ch05(),
  ...ch06(),
  ...ch07(),
  ...ch08(),
  ...ch09(),
  ...ch10(),
  ...ch11(),
  ...ch12(),
  ...ch13(),
  ...ch14(),
  ...ch15(),
  ...ch16(),
];

const doc = new Document({
  creator: "独立游戏开发者",
  title: "幸存者游戏策划案",
  description: "基于原始策划表格与项目实际内容整合而成的完整策划文档",
  numbering: {
    config: [
      {
        reference: "bullets",
        levels: [
          { level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 480, hanging: 260 } } } },
          { level: 1, format: LevelFormat.BULLET, text: "◦", alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 960, hanging: 260 } } } },
        ],
      },
      {
        reference: "numbers",
        levels: [
          { level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 480, hanging: 300 } } } },
        ],
      },
    ],
  },
  styles: {
    default: {
      document: { run: { font: "Microsoft YaHei", size: 21 } },
    },
    paragraphStyles: [
      {
        id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 36, bold: true, font: "Microsoft YaHei", color: COLOR.black },
        paragraph: {
          spacing: { before: 360, after: 240 }, outlineLevel: 0,
          border: { bottom: { style: BorderStyle.SINGLE, size: 12, color: COLOR.black, space: 6 } },
        },
      },
      {
        id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 28, bold: true, font: "Microsoft YaHei", color: COLOR.black },
        paragraph: { spacing: { before: 280, after: 160 }, outlineLevel: 1 },
      },
      {
        id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 24, bold: true, font: "Microsoft YaHei", color: COLOR.black },
        paragraph: { spacing: { before: 220, after: 120 }, outlineLevel: 2 },
      },
      {
        id: "Heading4", name: "Heading 4", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 22, bold: true, font: "Microsoft YaHei", color: COLOR.black },
        paragraph: { spacing: { before: 180, after: 100 }, outlineLevel: 3 },
      },
    ],
  },
  sections: [
    {
      properties: {
        page: {
          size: { width: 11906, height: 16838 }, // A4
          margin: { top: 1440, right: 1260, bottom: 1440, left: 1260 },
        },
      },
      children,
    },
  ],
});

Packer.toBuffer(doc).then((buffer) => {
  const outPath = path.join(__dirname, "..", "幸存者游戏策划案-整合版.docx");
  fs.writeFileSync(outPath, buffer);
  console.log("written:", outPath, buffer.length, "bytes");
});
