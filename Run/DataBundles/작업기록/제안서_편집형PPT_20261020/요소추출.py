from pathlib import Path
from collections import defaultdict
import json
import math
import pdfplumber
from PIL import Image
from pypdf import PdfReader

source = Path(r"C:\Users\ParkJongYoun\Downloads\RO_몬스터_레이스_제안서_가을목장반영본.pdf")
work = Path(__file__).parent
assets = work / "원본그림"
assets.mkdir(parents=True, exist_ok=True)
source_pages = work.parent / "제안서_PPT변환_20261020/원본페이지"


def color(value):
    if isinstance(value, (float, int)):
        rgb = [value] * 3
    elif isinstance(value, (list, tuple)):
        rgb = value * 3 if len(value) == 1 else value[:3]
    else:
        return None
    return "#" + "".join(f"{round(max(0, min(1, x)) * 255):02X}" for x in rgb)


def visible_chars(page):
    # These two original master-footer paragraphs are covered by a subsequent
    # full-page white rectangle in the PDF and are not visible in the source.
    return [c for c in page.chars if c.get("mcid") not in (1, 2)]


def text_lines(chars):
    by_baseline = defaultdict(list)
    for char in chars:
        by_baseline[round(char["matrix"][5], 1)].append(char)
    lines = []
    for baseline, row in by_baseline.items():
        row.sort(key=lambda c: c["x0"])
        segments = []
        segment = []
        for char in row:
            if segment:
                gap = char["x0"] - segment[-1]["x1"]
                threshold = min(char["size"], segment[-1]["size"]) * 1.05
                if gap > threshold:
                    segments.append(segment)
                    segment = []
            segment.append(char)
        if segment:
            segments.append(segment)
        for segment in segments:
            runs = []
            previous = None
            for char in segment:
                style = {
                    "size": round(char["size"], 3),
                    "bold": "Bold" in char["fontname"],
                    "color": color(char["non_stroking_color"]),
                }
                text = char["text"]
                if previous and not previous["text"].endswith(" ") and not text.startswith(" "):
                    gap = char["x0"] - previous["x1"]
                    if gap > min(char["size"], previous["size"]) * 0.13:
                        spaces = max(1, round(gap / (char["size"] * 0.352)))
                        text = " " * spaces + text
                if runs and runs[-1]["style"] == style:
                    runs[-1]["text"] += text
                else:
                    runs.append({"text": text, "style": style})
                previous = char
            lines.append({
                "text": "".join(run["text"] for run in runs),
                "x": min(c["x0"] for c in segment),
                "right": max(c["x1"] for c in segment),
                "top": min(c["top"] for c in segment),
                "baseline": 540 - segment[0]["matrix"][5],
                "size": max(c["size"] for c in segment),
                "runs": runs,
            })
    return sorted(lines, key=lambda line: (line["top"], line["x"]))


def within(obj, bounds):
    x0, y0, x1, y1 = bounds
    return (
        obj["x0"] >= x0 - 0.1 and obj["x1"] <= x1 + 0.1
        and obj["top"] >= y0 - 0.1 and obj["bottom"] <= y1 + 0.1
    )


reader = PdfReader(source)
pages = []
with pdfplumber.open(source) as pdf:
    for index, page in enumerate(pdf.pages):
        number = index + 1
        chars = visible_chars(page)
        table = None
        if number in (7, 11):
            if number == 7:
                xs = [44.25, 152.25, 531.75]
                ys = [187.5 + 41.25 * row for row in range(6)]
            else:
                xs = [39.75, 229.5, 744.0, 920.25]
                ys = [157.5 + 39 * row for row in range(9)]
            bounds = (xs[0], ys[0], xs[-1], ys[-1])
            cells = []
            for r in range(len(ys) - 1):
                cell_row = []
                for c in range(len(xs) - 1):
                    box = (xs[c], ys[r], xs[c+1], ys[r+1])
                    cell_chars = [char for char in chars if within(char, box)]
                    lines = text_lines(cell_chars)
                    matching_rect = next(rect for rect in page.rects if
                        abs(rect["x0"] - xs[c]) < 0.1 and
                        abs(rect["top"] - ys[r]) < 0.1 and
                        abs(rect["width"] - (xs[c+1] - xs[c])) < 0.1)
                    cell_row.append({
                        "text": "\n".join(line["text"] for line in lines),
                        "runs": lines[0]["runs"],
                        "size": lines[0]["size"],
                        "text_x": lines[0]["x"],
                        "text_top": lines[0]["top"],
                        "baseline": lines[0]["baseline"],
                        "fill": color(matching_rect["non_stroking_color"]),
                    })
                cells.append(cell_row)
            table = {"xs": xs, "ys": ys, "cells": cells}
            chars = [char for char in chars if not within(char, bounds)]

        rectangles = []
        # Rectangles before the second full-page white fill belong to the
        # hidden template. Retain only visible foreground rectangles.
        background_indices = [
            i for i, rect in enumerate(page.rects)
            if rect["width"] > 959 and rect["height"] > 539
        ]
        start = max(background_indices) + 1
        for rect in page.rects[start:]:
            if table and within(rect, bounds):
                continue
            fill = color(rect["non_stroking_color"]) if rect["fill"] else None
            if rect["fill"] and fill is None:
                # Cover-image gradients are included with the source image.
                continue
            rectangles.append({
                "x": rect["x0"], "y": rect["top"],
                "w": rect["width"], "h": rect["height"],
                "fill": fill,
                "stroke": color(rect["stroking_color"]) if rect["stroke"] else None,
                "lineWidth": rect["linewidth"],
            })
        lines = []
        for line in page.lines:
            if table and within(line, (bounds[0]-1, bounds[1]-1, bounds[2]+1, bounds[3]+1)):
                continue
            if line["linewidth"] <= 0:
                continue
            lines.append({
                "x": line["x0"], "y": line["top"],
                "w": line["width"], "h": line["height"],
                "color": color(line["stroking_color"]),
                "lineWidth": line["linewidth"],
            })

        images = []
        for image_index, image in enumerate(page.images, 1):
            destination = assets / f"{number:02d}쪽_그림_{image_index:02d}.png"
            x, y = max(0, image["x0"]), max(0, image["top"])
            w = min(960, image["x1"]) - x
            h = min(540, image["bottom"]) - y
            if number == 1:
                # Preserve the reference's baked fade and decorative overlays
                # for its two sidebar pictures only; central text stays native.
                rendered = Image.open(source_pages / f"페이지-{number:02d}.png")
                rendered.crop((
                    round(x * 4), round(y * 4),
                    round((x+w) * 4), round((y+h) * 4),
                )).save(destination)
            else:
                reader.pages[index].images["/" + image["name"]].image.save(destination)
            images.append({"x": x, "y": y, "w": w, "h": h, "path": str(destination)})

        pages.append({
            "number": number,
            "text": text_lines(chars),
            "rectangles": rectangles,
            "lines": lines,
            "images": images,
            "table": table,
        })

(work / "원본요소.json").write_text(
    json.dumps(pages, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps([
    {"page": p["number"], "text_boxes": len(p["text"]),
     "images": len(p["images"]), "native_table": bool(p["table"])}
    for p in pages
], ensure_ascii=False))
