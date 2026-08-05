import re
import os

def find_vietnamese_strings(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    strings = re.findall(r'"([^"]*)"', content)
    vi_chars = set('áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴĐ')
    
    vi_strings = []
    for s in strings:
        if any(c in vi_chars for c in s):
            vi_strings.append(s)
            
    return set(vi_strings)

dir_path = r'd:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\Views'
with open('strings_output.txt', 'w', encoding='utf-8') as out:
    for root, _, files in os.walk(dir_path):
        for f in files:
            if f.endswith('.cs') or f.endswith('.xaml'):
                path = os.path.join(root, f)
                res = find_vietnamese_strings(path)
                if res:
                    out.write(f"--- {f} ---\n")
                    for s in sorted(res):
                        out.write(f'"{s}"\n')
