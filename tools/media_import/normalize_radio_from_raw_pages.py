import csv
import re
import sys
import uuid
from pathlib import Path


KNOWN_STATIONS = [
    "Smile 90.4FM",
    "Algoa FM",
    "Jozi FM",
    "Kaya 959",
    "Kaya",
    "Metro FM",
    "Good Hope FM",
    "5FM",
    "Ikwekwezi FM",
    "Lesedi FM",
    "Ligwalagwala FM",
    "Motsweding FM",
    "Munghana Lonene FM",
    "Phalaphala FM",
    "Thobela FM",
    "TruFM",
    "Ukhozi FM",
    "Umhlobo Wenene FM",
    "XK FM",
    "Channel Africa",
    "Lotus FM",
    "Radio 2000",
    "RSG",
    "SAfm",
    "SABC",
]

KNOWN_PACKAGE_LABELS = [
    "WORKZONE",
    "WEEKEND PACKAGE",
    "RETAIL PACKAGE",
    "POWERWEEK",
    "LUNCTIME",
    "LUNCHTIME",
    "LUNCH TIME",
    "IMPACT PLUS PACKAGE",
    "GENERIC & STREAMING PRE-ROLL PACKAGE - 6 Months",
    "ALGOA CLUB - PLAN A - MON TO SUN - 2WKS P/M - 6MTHS",
]

PACKAGE_LABEL_NORMALIZATIONS = {
    "LUNCTIME": "LUNCHTIME",
    "LUNCH TIME": "LUNCHTIME",
}


def clean_text(value: str) -> str:
    value = value.replace("\x0c", "\n").replace("\u00a0", " ")
    value = re.sub(r"[ \t\r]+", " ", value)
    value = re.sub(r"\n{3,}", "\n\n", value)
    return value.strip()


def find_station(text: str, source_file: str) -> str:
    upper = text.upper()
    for candidate in KNOWN_STATIONS:
        if candidate.upper() in upper:
            return candidate
    return Path(source_file).stem


def fallback_package_name(source_file: str) -> str:
    stem = Path(source_file).stem
    stem = re.sub(r"\s+\(\d+\)$", "", stem)
    stem = stem.replace("  ", " ").strip(" -")
    return stem


def cleanup_display_name(value: str) -> str:
    value = clean_text(value)
    value = value.replace("%", " ")
    value = re.sub(r"\s{2,}", " ", value)
    value = re.sub(r"\s+-\s+", " - ", value)
    value = re.sub(r"\s+([,.)])", r"\1", value)
    value = value.strip(" -_/")
    return value


def extract_campaign_line_name(text: str) -> str | None:
    match = re.search(
        r"CONTACT\s+NAME\s+CAMPAIGN\s+([A-Z0-9&/+'\"().,\-\s]{3,120}?)(?:\s{2,}(?:SPONSOR|LIVE|RECORDED|CONTRACT|DISCOUNT)|\n)",
        text,
        re.IGNORECASE,
    )
    if not match:
        return None

    candidate = cleanup_display_name(match.group(1))
    if not candidate:
        return None

    candidate_upper = candidate.upper()
    blocked = {
        "CAMPAIGN",
        "CONTACT NAME",
        "AIRTIME APP",
        "KAYA 959",
    }
    if candidate_upper in blocked:
        return None

    return candidate


def extract_known_package_label(text: str) -> str | None:
    upper = text.upper()
    for label in KNOWN_PACKAGE_LABELS:
        if label in upper:
            return PACKAGE_LABEL_NORMALIZATIONS.get(label, label)
    return None


def clean_package_name(candidate: str, source_file: str) -> str:
    value = clean_text(candidate)
    value = re.sub(r"\bCONTACT NAME\b", "", value, flags=re.IGNORECASE)
    value = re.sub(r"\bCAMPAIGN\b", "", value, flags=re.IGNORECASE)
    value = re.sub(r"\bAIRTIME APP\b", "", value, flags=re.IGNORECASE)
    value = re.sub(r"\bSPONSOR\s*[+X]?\s*\+?\s*\d+%?\b", "", value, flags=re.IGNORECASE)
    value = cleanup_display_name(value)

    bad_markers = (
        "CONTACT TEL",
        "CONTACT EMAIL",
        "HOLDING CO.",
        "AD LENGTH",
        "DISCOUNT",
    )

    if not value or any(marker in value.upper() for marker in bad_markers):
        return fallback_package_name(source_file)

    return value


def extract_package_name(text: str, source_file: str) -> str:
    known_label = extract_known_package_label(text)
    if known_label:
        return clean_package_name(known_label, source_file)

    campaign_name = extract_campaign_line_name(text)
    if campaign_name:
        return clean_package_name(campaign_name, source_file)

    lines = [line.strip() for line in clean_text(text).splitlines() if line.strip()]
    for index, line in enumerate(lines[:20]):
        if "INVESTMENT SUMMARY" in line.upper() and index > 0:
            return clean_package_name(lines[index - 1], source_file)
    for line in lines[:12]:
        if "PACKAGE" in line.upper() and len(line) < 160:
            return clean_package_name(line, source_file)
    return fallback_package_name(source_file)


def extract_duration_seconds(text: str) -> int:
    match = re.search(r"AD LENGTH\s+(\d+)\s+SECONDS", text, re.IGNORECASE)
    if match:
        return int(match.group(1))

    match = re.search(r'(\d+)\s*"', text)
    if match:
        return int(match.group(1))

    return 30


def extract_exposure_per_month(text: str) -> str:
    patterns = [
        r"(\d+)\s*x\s*COMMERCIALS\s*PER\s*MONTH",
        r"(\d+)\s*x\s*30\"",
        r"Cost Per Month\s+(\d+)",
    ]
    for pattern in patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            return match.group(1)
    return ""


def extract_spots_count(text: str) -> int | None:
    exposure_text = extract_exposure_per_month(text)
    if exposure_text.isdigit():
        return int(exposure_text)

    total_spots = parse_int(r"TOTAL\s+NO\s+OF\s+SPOTS\s+(\d+)", text)
    if total_spots is not None:
        return total_spots

    matches = re.findall(r"TOTAL\s+INVOICE\s+R", text, re.IGNORECASE)
    if matches:
        return parse_int(r"(\d+)\s*(?:x|spots|commercials)\b", text)

    return parse_int(r"(\d+)\s*(?:commercials|spots)\b", text)


def extract_windows(text: str) -> tuple[str, str, str]:
    matches = re.findall(r"\d{2}:\d{2}\s*-\s*\d{2}:\d{2}", text)
    unique_matches = list(dict.fromkeys(window.replace(" - ", "-").replace(" -", "-").replace("- ", "-") for window in matches))

    weekday = unique_matches[0] if len(unique_matches) > 0 else ""
    saturday = unique_matches[1] if len(unique_matches) > 1 else ""
    sunday = unique_matches[2] if len(unique_matches) > 2 else ""

    return weekday, saturday, sunday


def extract_live_reads_allowed(text: str) -> str:
    upper = text.upper()
    if "NO LIVE READS ACCEPTED" in upper:
        return "false"
    if "LIVE READ" in upper:
        return "true"
    return ""


def extract_terms(text: str) -> str:
    match = re.search(
        r"TERMS?\s*&\s*CONDITIONS\s*:(.*?)(?:\n\n|PRE-RECORDED SHOWS|AIRTIME APP|$)",
        text,
        re.IGNORECASE | re.DOTALL,
    )
    if not match:
        return ""

    block = clean_text(match.group(1))
    parts = []
    for piece in re.split(r"\n|(?=\d+\.)", block):
        piece = piece.strip(" -")
        if piece:
            parts.append(piece)
    return " | ".join(dict.fromkeys(parts))


def extract_notes(text: str) -> str:
    notes: list[str] = []

    for pattern in [
        r"PLEASE NOTE\s*:(.*?)(?:\n\n|PRE-RECORDED SHOWS|$)",
        r"ALL BOOKINGS ARE SUBJECT TO .*?(?:\n|$)",
        r"The above investment is based on .*?(?:\n|$)",
    ]:
        for match in re.finditer(pattern, text, re.IGNORECASE | re.DOTALL):
            value = clean_text(match.group(1) if match.lastindex else match.group(0))
            if value:
                notes.append(value)

    return " | ".join(dict.fromkeys(notes))


def parse_money(text: str) -> float | None:
    match = re.search(r"R\s*([\d\s,]+(?:\.\d+)?)", text)
    if not match:
        return None

    return parse_money_value(match.group(1))


def parse_money_value(value: str) -> float | None:
    cleaned = value.strip()
    if not cleaned:
        return None

    cleaned = re.sub(r"[^\d,.\s]", "", cleaned)
    cleaned = re.sub(r"\s+", "", cleaned)

    if "," in cleaned and "." not in cleaned:
        cleaned = cleaned.replace(",", ".")
    elif "," in cleaned and "." in cleaned:
        cleaned = cleaned.replace(",", "")

    if not cleaned:
        return None

    return round(float(cleaned), 2)


def extract_amounts_for_label(text: str, label: str) -> list[float]:
    pattern = rf"{label}\s+R\s*([\d\s,]+(?:\.\d+)?)"
    return [amount for amount in (parse_money_value(match) for match in re.findall(pattern, text, re.IGNORECASE)) if amount is not None]


def extract_total_invoice_amounts(text: str) -> list[float]:
    return extract_amounts_for_label(text, r"TOTAL\s+INVOICE")


def extract_total_investment_amounts(text: str) -> list[float]:
    return extract_amounts_for_label(text, r"TOTAL\s+INVESTMENT")


def extract_total_excl_vat_amounts(text: str) -> list[float]:
    return extract_amounts_for_label(text, r"TOTAL\s+EXCL\s+VAT")


def extract_primary_invoice_amount(text: str) -> float | None:
    investment_amounts = extract_total_investment_amounts(text)
    if investment_amounts:
        return investment_amounts[-1]

    invoice_amounts = extract_total_invoice_amounts(text)
    if invoice_amounts:
        return invoice_amounts[0]

    excl_vat_amounts = extract_total_excl_vat_amounts(text)
    if excl_vat_amounts:
        return excl_vat_amounts[0]

    return None


def parse_int(pattern: str, text: str) -> int | None:
    match = re.search(pattern, text, re.IGNORECASE)
    if not match:
        return None
    return int(match.group(1).replace(",", ""))


def is_likely_radio_package(source_file: str, text: str) -> bool:
    upper = f"{source_file}\n{text}".upper()
    markers = ["JOZI FM", "ALGOA FM", "SMILE", "KAYA", "PRE-RECORDED SHOWS", "LIVE READ", "CLUB PACKAGE"]
    return any(marker in upper for marker in markers)


def is_likely_sabc(source_file: str, text: str) -> bool:
    upper_source = source_file.upper()
    upper_text = text.upper()

    if "SABC_TV" in upper_source or "SPORTS_RATE" in upper_source or "SABC SPORT" in upper_text:
        return False

    radio_markers = [
        "RADIO RATES",
        "SABC RADIO",
        "METRO FM",
        "GOOD HOPE FM",
        "LOTUS FM",
        "RADIO 2000",
        "SAFM",
        "RSG",
        "UKHOZI FM",
        "5FM",
    ]

    combined = f"{upper_source}\n{upper_text}"
    return any(marker in combined for marker in radio_markers)


def build_radio_row(source_file: str, page_text: str) -> dict[str, str]:
    weekday, saturday, sunday = extract_windows(page_text)
    spots_count = extract_spots_count(page_text)
    total_invoice = extract_primary_invoice_amount(page_text)
    avg_cost_per_spot = round(total_invoice / spots_count, 2) if total_invoice is not None and spots_count and spots_count > 0 else None
    return {
        "source_file": source_file,
        "station": find_station(page_text, source_file),
        "package_name": extract_package_name(page_text, source_file),
        "ad_length_seconds": str(extract_duration_seconds(page_text)),
        "exposure_per_month_text": extract_exposure_per_month(page_text),
        "spots_count": str(spots_count) if spots_count is not None else "",
        "total_invoice_zar": f"{total_invoice:.2f}" if total_invoice is not None else "",
        "package_cost_zar": f"{total_invoice:.2f}" if total_invoice is not None else "",
        "avg_cost_per_spot_zar": f"{avg_cost_per_spot:.2f}" if avg_cost_per_spot is not None else "",
        "monday_friday_windows": weekday,
        "saturday_windows": saturday,
        "sunday_windows": sunday,
        "live_reads_allowed": extract_live_reads_allowed(page_text),
        "terms_excerpt": extract_terms(page_text),
        "notes": extract_notes(page_text),
        "raw_grid_excerpt": clean_text(page_text)[:5000],
    }


def build_sabc_row(source_file: str, page_text: str) -> dict[str, str]:
    product_name = find_station(page_text, source_file)
    package_cost = parse_money(page_text)
    spots_count = parse_int(r"(?:NO OF SPOTS|No Spots)\s+([\d,]+)", page_text)
    avg_cost = parse_money(re.search(r"(?:AVER COST PER SPOT|AVE CPP)\s+R([\d\s,]+(?:\.\d+)?)", page_text, re.IGNORECASE).group(0)) if re.search(r"(?:AVER COST PER SPOT|AVE CPP)\s+R([\d\s,]+(?:\.\d+)?)", page_text, re.IGNORECASE) else None
    exposure_value = parse_money(re.search(r"(?:EXPOSURE VALUE|PACKAGE VALUE)\s+R([\d\s,]+(?:\.\d+)?)", page_text, re.IGNORECASE).group(0)) if re.search(r"(?:EXPOSURE VALUE|PACKAGE VALUE)\s+R([\d\s,]+(?:\.\d+)?)", page_text, re.IGNORECASE) else None

    audience_match = re.search(r"(SEM\s*[\d\-]+)", page_text, re.IGNORECASE)
    date_match = re.search(r"(JULY\s*2024\s*-\s*JUNE\s*2025|APRIL\s*-\s*JUNE\s*2025|\d{2}\s*[A-Z]{3}\s*-\s*\d{2}\s*[A-Z]{3}\s*\d{4})", page_text, re.IGNORECASE)

    return {
        "source_file": source_file,
        "channel_type": "Radio",
        "product_name": product_name,
        "package_cost_zar": f"{package_cost:.2f}" if package_cost is not None else "",
        "spots_count": str(spots_count) if spots_count is not None else "",
        "avg_cost_per_spot_zar": f"{avg_cost:.2f}" if avg_cost is not None else "",
        "exposure_value_zar": f"{exposure_value:.2f}" if exposure_value is not None else "",
        "audience_segment": audience_match.group(1).upper() if audience_match else "",
        "date_range_text": date_match.group(1) if date_match else "",
        "notes": extract_notes(page_text),
        "raw_excerpt": clean_text(page_text)[:5000],
    }


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python tools/media_import/normalize_radio_from_raw_pages.py <raw_pdf_pages.csv> [output_dir]")
        return 1

    input_path = Path(sys.argv[1]).resolve()
    output_dir = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else input_path.parent
    output_dir.mkdir(parents=True, exist_ok=True)

    radio_rows: list[dict[str, str]] = []
    sabc_rows: list[dict[str, str]] = []
    seen_radio: set[str] = set()
    seen_sabc: set[str] = set()

    with input_path.open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        for row in reader:
            source_file = (row.get("source_file") or "").strip()
            page_text = clean_text(row.get("page_text") or row.get("text") or "")

            if not source_file or not page_text:
                continue

            page_key = f"{source_file}::{hash(page_text)}"

            if is_likely_radio_package(source_file, page_text) and page_key not in seen_radio:
                radio_rows.append(build_radio_row(source_file, page_text))
                seen_radio.add(page_key)

            if is_likely_sabc(source_file, page_text) and page_key not in seen_sabc:
                sabc_rows.append(build_sabc_row(source_file, page_text))
                seen_sabc.add(page_key)

    radio_output = output_dir / "radio_slot_grid_seed.csv"
    sabc_output = output_dir / "sabc_rate_table_seed.csv"

    with radio_output.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "source_file",
                "station",
                "package_name",
                "ad_length_seconds",
                "exposure_per_month_text",
                "spots_count",
                "total_invoice_zar",
                "package_cost_zar",
                "avg_cost_per_spot_zar",
                "monday_friday_windows",
                "saturday_windows",
                "sunday_windows",
                "live_reads_allowed",
                "terms_excerpt",
                "notes",
                "raw_grid_excerpt",
            ],
        )
        writer.writeheader()
        writer.writerows(radio_rows)

    with sabc_output.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=[
                "source_file",
                "channel_type",
                "product_name",
                "package_cost_zar",
                "spots_count",
                "avg_cost_per_spot_zar",
                "exposure_value_zar",
                "audience_segment",
                "date_range_text",
                "notes",
                "raw_excerpt",
            ],
        )
        writer.writeheader()
        writer.writerows(sabc_rows)

    print(f"Wrote {len(radio_rows)} radio rows to {radio_output}")
    print(f"Wrote {len(sabc_rows)} SABC rows to {sabc_output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
