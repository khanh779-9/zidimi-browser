import os, re

def get_keys_from_cs(directory):
    keys = set()
    regex = re.compile(r'LanguageManager\.Instance\["(.*?)"\]')
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                with open(os.path.join(root, file), "r", encoding="utf-8", errors="ignore") as f:
                    content = f.read()
                    matches = regex.findall(content)
                    for match in matches:
                        keys.add(match)
    return keys

def get_keys_from_lng(filepath):
    keys = set()
    current_section = ""
    if not os.path.exists(filepath): return keys
    with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith(";") or line.startswith("#"): continue
            if line.startswith("[") and line.endswith("]"):
                current_section = line[1:-1]
                continue
            if "=" in line:
                key = line.split("=")[0].strip()
                full_key = key if "_" in key else f"{current_section}_{key}"
                keys.add(full_key)
    return keys

cs_keys = get_keys_from_cs(r"d:\Data\Tailieu\Projects\C#\Heco_Browser")
lang_dir = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\language"

for lang in ["vi-VN.lng", "en-US.lng", "zh-CN.lng"]:
    lng_keys = get_keys_from_lng(os.path.join(lang_dir, lang))
    missing = cs_keys - lng_keys
    print(f"Missing in {lang}: {len(missing)}")
    if missing:
        print(missing)
