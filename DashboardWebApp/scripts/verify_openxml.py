#!/usr/bin/env python3
"""Parse test4.xlsx the same way OpenXmlExcelReader does (ZIP + XML)."""
from __future__ import annotations

import zipfile
from datetime import datetime, timedelta
from pathlib import Path
from xml.etree import ElementTree as ET

MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
PKG_REL = "{http://schemas.openxmlformats.org/package/2006/relationships}"
OFFICE_REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
XLSX = Path(__file__).resolve().parents[1] / "App_Data" / "test4.xlsx"


def col_index(cell_ref: str) -> int:
    index = 0
    for char in cell_ref:
        if not char.isalpha():
            break
        index = index * 26 + (ord(char.upper()) - 64)
    return index - 1


def normalize_part_path(target: str) -> str:
    normalized = target.replace("\\", "/").strip().lstrip("/")
    if normalized.lower().startswith("xl/"):
        return normalized
    return "xl/" + normalized.lstrip("/")


def looks_like_date_format(code: str) -> bool:
    normalized = (code or "").lower()
    return "y" in normalized or "d" in normalized or "yy" in normalized or "dd" in normalized


def is_builtin_date(num_fmt_id: int) -> bool:
    return (14 <= num_fmt_id <= 22) or (27 <= num_fmt_id <= 36) or (45 <= num_fmt_id <= 47) or (50 <= num_fmt_id <= 58)


def shared_strings(zf: zipfile.ZipFile) -> list[str]:
    if "xl/sharedStrings.xml" not in zf.namelist():
        return []
    root = ET.fromstring(zf.read("xl/sharedStrings.xml"))
    values = []
    for si in root.findall(f"{MAIN}si"):
        values.append("".join(node.text or "" for node in si.iter(f"{MAIN}t")).strip())
    return values


def date_style_indexes(zf: zipfile.ZipFile) -> set[int]:
    if "xl/styles.xml" not in zf.namelist():
        return set()
    root = ET.fromstring(zf.read("xl/styles.xml"))
    custom_dates = set()
    num_fmts = root.find(f"{MAIN}numFmts")
    if num_fmts is not None:
        for num_fmt in num_fmts.findall(f"{MAIN}numFmt"):
            fmt_id = int(num_fmt.attrib.get("numFmtId", "-1"))
            if looks_like_date_format(num_fmt.attrib.get("formatCode", "")):
                custom_dates.add(fmt_id)
    result = set()
    cell_xfs = root.find(f"{MAIN}cellXfs")
    if cell_xfs is None:
        return result
    for index, xf in enumerate(cell_xfs.findall(f"{MAIN}xf")):
        num_fmt_id = int(xf.attrib.get("numFmtId", "0"))
        if is_builtin_date(num_fmt_id) or num_fmt_id in custom_dates:
            result.add(index)
    return result


def sheet_names(zf: zipfile.ZipFile) -> list[str]:
    root = ET.fromstring(zf.read("xl/workbook.xml"))
    sheets = root.find(f"{MAIN}sheets")
    return [sheet.attrib["name"] for sheet in sheets.findall(f"{MAIN}sheet")]


def worksheet_path(zf: zipfile.ZipFile, name: str) -> str:
    workbook = ET.fromstring(zf.read("xl/workbook.xml"))
    rels = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
    rel_map = {
        rel.attrib["Id"]: normalize_part_path(rel.attrib["Target"])
        for rel in rels.findall(f"{PKG_REL}Relationship")
    }
    for sheet in workbook.find(f"{MAIN}sheets").findall(f"{MAIN}sheet"):
        if sheet.attrib["name"] == name:
            return rel_map[sheet.attrib[f"{OFFICE_REL}id"]]
    raise KeyError(name)


def read_cell(cell, strings: list[str], date_styles: set[int]) -> str:
    cell_type = cell.attrib.get("t")
    if cell_type == "inlineStr":
        inline = cell.find(f"{MAIN}is")
        if inline is None:
            return ""
        return "".join(node.text or "" for node in inline.iter(f"{MAIN}t")).strip()
    value_node = cell.find(f"{MAIN}v")
    raw = value_node.text.strip() if value_node is not None and value_node.text else ""
    if cell_type == "s":
        return strings[int(raw)]
    style = cell.attrib.get("s")
    if style is not None and int(style) in date_styles:
        oa = float(raw)
        if 20000 < oa < 80000:
            return (datetime(1899, 12, 30) + timedelta(days=oa)).strftime("%Y-%m-%d")
    return raw


def read_sheet(zf: zipfile.ZipFile, name: str) -> list[list[str]]:
    strings = shared_strings(zf)
    dates = date_style_indexes(zf)
    root = ET.fromstring(zf.read(worksheet_path(zf, name)))
    rows = []
    for row in root.find(f"{MAIN}sheetData").findall(f"{MAIN}row"):
        cells = {}
        max_index = -1
        for cell in row.findall(f"{MAIN}c"):
            index = col_index(cell.attrib.get("r", ""))
            max_index = max(max_index, index)
            cells[index] = read_cell(cell, strings, dates)
        rows.append([cells.get(i, "") for i in range(max_index + 1)])
    return rows


def main():
    with zipfile.ZipFile(XLSX) as zf:
        names = sheet_names(zf)
        assert names == ["สินค้า", "ลูกค้า", "พนักงาน", "การขาย"], names
        products = read_sheet(zf, "สินค้า")
        customers = read_sheet(zf, "ลูกค้า")
        employees = read_sheet(zf, "พนักงาน")
        sales = read_sheet(zf, "การขาย")

    assert products[0][0] == "ProductID"
    assert products[1][:3] == ["P001", "ปูนซีเมนต์ Portland", "ปูน"]
    assert customers[1][0] == "C001"
    assert employees[1][0] == "E001"
    assert sales[1][0] == "S001"
    assert sales[1][1] == "2026-08-01", sales[1][1]
    assert len(products) == 11
    assert len(sales) == 13

    revenue = sum(float(row[7]) for row in sales[1:])
    stock_value = sum(float(row[3]) * int(float(row[5])) for row in products[1:])
    assert abs(revenue - 51635) < 0.01, revenue
    assert abs(stock_value - 311900) < 0.01, stock_value
    print("openxml zip/xml parse ok")
    print("first product", products[1])
    print("first sale", sales[1])
    print(f"revenue={revenue} stock_value={stock_value}")


if __name__ == "__main__":
    main()
