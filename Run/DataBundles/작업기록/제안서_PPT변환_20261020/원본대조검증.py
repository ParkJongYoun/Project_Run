from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import hashlib
import io
import json
import posixpath
from PIL import Image

root = Path(r"D:\Project_Run\Run\DataBundles")
work = root / "작업기록/제안서_PPT변환_20261020"
pptx = root / "기획/제안서_PPT변환/RO_몬스터_레이스_제안서_가을목장반영본.pptx"
ns = {
    "p": "http://schemas.openxmlformats.org/presentationml/2006/main",
    "a": "http://schemas.openxmlformats.org/drawingml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
}
checks = []
with ZipFile(pptx) as archive:
    presentation = ET.fromstring(archive.read("ppt/presentation.xml"))
    assert len(presentation.find("p:sldIdLst", ns)) == 11
    assert presentation.find("p:sldSz", ns).attrib["cx"] == "12192000"
    assert presentation.find("p:sldSz", ns).attrib["cy"] == "6858000"
    for number, original in enumerate(sorted((work / "원본페이지").glob("*.png")), 1):
        slide = ET.fromstring(archive.read(f"ppt/slides/slide{number}.xml"))
        pictures = slide.findall(".//p:pic", ns)
        assert len(pictures) == 1, number
        picture = pictures[0]
        offset = picture.find(".//a:xfrm/a:off", ns)
        extent = picture.find(".//a:xfrm/a:ext", ns)
        assert offset.attrib == {"x": "0", "y": "0"}, number
        assert extent.attrib == {"cx": "12192000", "cy": "6858000"}, number
        rid = picture.find(".//a:blip", ns).attrib["{" + ns["r"] + "}embed"]
        relationships = ET.fromstring(
            archive.read(f"ppt/slides/_rels/slide{number}.xml.rels")
        )
        target = next(
            rel.attrib["Target"]
            for rel in relationships
            if rel.attrib["Id"] == rid
        )
        asset_path = posixpath.normpath(
            posixpath.join("ppt/slides", target)
        ).lstrip("/")
        embedded = archive.read(asset_path)
        source = original.read_bytes()
        assert hashlib.sha256(embedded).digest() == hashlib.sha256(source).digest(), number
        size = Image.open(io.BytesIO(embedded)).size
        assert size == (3840, 2160), (number, size)
        assert (work / "검증이미지" / f"슬라이드_{number:02d}.png").exists()
        checks.append({
            "page": number,
            "source_image_byte_identical": True,
            "image_size": size,
            "full_slide_no_crop": True,
        })

report = {
    "slides": 11,
    "aspect_ratio": "16:9",
    "pptx_bytes": pptx.stat().st_size,
    "checks": checks,
}
(work / "원본대조검증.json").write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
)
print(json.dumps({
    "slides": 11,
    "all_embedded_images_identical_to_source": True,
    "image_size": [3840, 2160],
    "size_mb": round(pptx.stat().st_size / 1000000, 1),
}, ensure_ascii=False))
