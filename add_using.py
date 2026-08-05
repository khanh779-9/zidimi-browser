import os

base_dir = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\Views"

for root, _, files in os.walk(base_dir):
    for f in files:
        if f.endswith('.cs'):
            path = os.path.join(root, f)
            with open(path, 'r', encoding='utf-8') as file:
                content = file.read()
            
            if 'LanguageManager' in content and 'using Heco.Browser.Infrastructure;' not in content:
                content = 'using Heco.Browser.Infrastructure;\n' + content
                with open(path, 'w', encoding='utf-8') as file:
                    file.write(content)
