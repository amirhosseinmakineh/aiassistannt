#!/usr/bin/env python3
"""Static contract check for the Realtime composite receptionist instructions."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
config = json.loads((ROOT / "AiCall/appsettings.json").read_text(encoding="utf-8"))
prompt = config["OpenAi"]["Instructions"]

# All scenarios must remain model context, not application keyword routing.
scenario_numbers = {int(value) for value in re.findall(r"(?m)^(\d+)\. ", prompt)}
assert scenario_numbers == set(range(1, 41)), "The prompt must contain all 40 numbered scenarios"

# Representative intent/safety contracts: alternatives test meaning, not an exact reply.
contracts = {
    "price": ("هزینه", "عدد نساز", "تعرفه معتبر"),
    "number_of_teeth": ("تعداد دندان", "طرح لبخند", "معاینه"),
    "natural_shade": ("ظاهر طبیعی", "رنگ و فرم طبیعی"),
    "very_white_shade": ("سفیدی زیاد", "بدون قضاوت"),
    "brand": ("برند مصرفی", "هرگز برند نساز"),
    "lifespan": ("طول عمر", "تضمین نده", "بهداشت"),
    "staining": ("تغییر رنگ", "نگهداری"),
    "coffee": ("قهوه", "لکه"),
    "smoking": ("سیگار", "سلامت دهان"),
    "laminate_comparison": ("کامپوزیت یا لمینت", "خنثی", "معاینه"),
    "tooth_shaving": ("تراش", "اصلاً تراش ندارد", "طرح پزشک"),
    "pain": ("درد", "تضمین بی‌دردی نده"),
    "duration": ("مدت انجام", "زمان دقیق نساز"),
    "crooked_teeth": ("دندان کمی کج", "ارتودنسی", "تشخیص نده"),
    "gap": ("فاصله دندان‌ها", "بایت"),
    "broken_composite": ("پریدن تکه کامپوزیت", "درد یا ضربه"),
    "replacement": ("تعویض کامپوزیت قدیمی", "دندان زیر آن"),
    "polishing": ("پولیش", "نگهداری"),
    "gum_concern": ("نگرانی لثه", "تضمین نکن"),
    "decay": ("پوسیدگی", "پیش از برنامه زیبایی"),
    "consultation": ("فقط مشاوره", "فشار نیاور"),
}
for intent, required_phrases in contracts.items():
    missing = [phrase for phrase in required_phrases if phrase not in prompt]
    assert not missing, f"{intent} is missing semantic guardrails: {missing}"

for safety_rule in ("تشخیص پزشکی", "نسخه دارویی", "هیچ قیمت", "هیچ زمان آزادی", "روال فوریت موجود"):
    assert safety_rule in prompt, f"Missing safety rule: {safety_rule}"

assert "فقط یک سؤال" in prompt and "به خاطر بسپار" in prompt
print(f"OK: 40 scenarios and {len(contracts)} representative intent contracts checked")
print(f"Static instructions: {len(prompt)} characters, {len(prompt.encode('utf-8'))} UTF-8 bytes")
