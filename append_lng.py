import os

translations = {
    'Menu_History': {'vi': 'Lịch sử', 'en': 'History', 'zh': '历史记录'},
    'History_Desc': {'vi': 'Trang web bạn đã ghé thăm gần đây', 'en': 'Websites you visited recently', 'zh': '您最近访问的网站'},
    'Common_ClearAll': {'vi': 'Xoá tất cả', 'en': 'Clear all', 'zh': '全部清除'},
    
    'Downloads_Title': {'vi': 'Tải xuống', 'en': 'Downloads', 'zh': '下载'},
    'Downloads_Desc': {'vi': 'Các file bạn đã tải', 'en': 'Files you have downloaded', 'zh': '您下载的文件'},
    'Downloads_Empty': {'vi': 'Chưa có file tải nào', 'en': 'No downloads yet', 'zh': '暂无下载内容'},
    'Downloads_EmptyDesc': {'vi': 'Các file bạn tải sẽ xuất hiện ở đây.', 'en': 'Files you download will appear here.', 'zh': '您下载的文件将显示在此处。'},

    'Bookmarks_Title': {'vi': 'Bookmark', 'en': 'Bookmarks', 'zh': '书签'},
    'Bookmarks_Desc': {'vi': 'Các trang bạn đã lưu', 'en': 'Pages you have saved', 'zh': '您保存的页面'},
    
    'Tab_ScrollLeft': {'vi': 'Cuộn sang trái', 'en': 'Scroll left', 'zh': '向左滚动'},
    'Tab_Mute': {'vi': 'Tắt/Bật tiếng', 'en': 'Mute/Unmute', 'zh': '静音/取消静音'},
    'Tab_ScrollRight': {'vi': 'Cuộn sang phải', 'en': 'Scroll right', 'zh': '向右滚动'},
    'Tab_List': {'vi': 'Danh sách tab', 'en': 'Tab list', 'zh': '标签页列表'},

    'Login_Title': {'vi': 'Đăng nhập để đồng bộ', 'en': 'Sign in to sync', 'zh': '登录以同步'},
    'Login_Desc': {'vi': 'Lịch sử, mật khẩu và dấu trang của bạn sẽ được an toàn.', 'en': 'Your history, passwords, and bookmarks will be safe.', 'zh': '您的历史记录、密码和书签将会安全。'},
    'Login_Email': {'vi': 'Email', 'en': 'Email', 'zh': '电子邮件'},
    'Login_Password': {'vi': 'Mật khẩu', 'en': 'Password', 'zh': '密码'},

    'Clear_Title': {'vi': 'Xoá dữ liệu duyệt web', 'en': 'Clear browsing data', 'zh': '清除浏览数据'},
    'Clear_TimeRange': {'vi': 'Phạm vi thời gian:', 'en': 'Time range:', 'zh': '时间范围：'},
    'Clear_HistoryDesc': {'vi': 'Xoá lịch sử duyệt web từ thiết bị này.', 'en': 'Clears browsing history from this device.', 'zh': '从此设备清除浏览历史记录。'},
    'Clear_CookiesDesc': {'vi': 'Đăng xuất bạn khỏi hầu hết các trang web.', 'en': 'Signs you out of most websites.', 'zh': '让您退出大多数网站。'},
    'Clear_CacheDesc': {'vi': 'Giải phóng không gian lưu trữ.', 'en': 'Frees up storage space.', 'zh': '释放存储空间。'},

    'Data_Title': {'vi': 'Quản lý dữ liệu', 'en': 'Manage data', 'zh': '管理数据'},
    'Data_Add': {'vi': 'Thêm mới', 'en': 'Add new', 'zh': '添加新项'},

    'Pref_General': {'vi': 'Chung', 'en': 'General', 'zh': '常规'},
    'Pref_GeneralDesc': {'vi': 'Cài đặt chung cho trình duyệt', 'en': 'General settings for the browser', 'zh': '浏览器的常规设置'},
    'Pref_StartupPage': {'vi': 'Trang khi mở tab mới', 'en': 'New tab page', 'zh': '新标签页'},
    'Pref_HomeUrl': {'vi': 'URL trang chủ', 'en': 'Homepage URL', 'zh': '主页 URL'},
    
    'Pref_DefaultEngine': {'vi': 'Công cụ tìm kiếm mặc định', 'en': 'Default search engine', 'zh': '默认搜索引擎'},
    'Pref_SelectEngine': {'vi': 'Chọn engine tìm kiếm', 'en': 'Select search engine', 'zh': '选择搜索引擎'},
    'Pref_OnStartup': {'vi': 'Khi khởi động', 'en': 'On startup', 'zh': '启动时'},
    'Pref_StartupAction': {'vi': 'Chọn hành động khởi động', 'en': 'Select startup action', 'zh': '选择启动操作'},
    'Pref_StartupNewPage': {'vi': 'Mở trang mới', 'en': 'Open the New Tab page', 'zh': '打开新标签页'},
    'Pref_StartupContinue': {'vi': 'Tiếp tục từ nơi đã dừng', 'en': 'Continue where you left off', 'zh': '从上次停下的地方继续'},
    'Pref_StartupSpecific': {'vi': 'Mở tập trang cụ thể', 'en': 'Open a specific page or set of pages', 'zh': '打开特定网页或一组网页'},

    'Pref_Profile': {'vi': 'Bạn và Heco', 'en': 'You and Heco', 'zh': '您与 Heco'},
    'Pref_ProfileDesc': {'vi': 'Quản lý hồ sơ và đồng bộ hoá', 'en': 'Manage profiles and sync', 'zh': '管理配置文件和同步'},
    'Pref_Logout': {'vi': 'Đăng xuất', 'en': 'Sign out', 'zh': '退出登录'},
    'Pref_Login': {'vi': 'Đăng nhập', 'en': 'Sign in', 'zh': '登录'},
    'Pref_SyncData': {'vi': 'Đồng bộ dữ liệu', 'en': 'Sync data', 'zh': '同步数据'},
    'Pref_SyncDesc': {'vi': 'Đăng nhập để đồng bộ lịch sử, dấu trang và mật khẩu', 'en': 'Sign in to sync history, bookmarks and passwords', 'zh': '登录以同步历史记录、书签和密码'},
    'Pref_CurrentProfile': {'vi': 'Hồ sơ hiện tại', 'en': 'Current profile', 'zh': '当前配置文件'},
    'Pref_ProfileApplyDesc': {'vi': 'Đổi profile sẽ áp dụng khi mở Tab mới', 'en': 'Changing profile will apply when opening a new Tab', 'zh': '更改配置文件将在打开新标签页时应用'},
    'Pref_AddProfile': {'vi': 'Thêm hồ sơ', 'en': 'Add profile', 'zh': '添加配置文件'},
    
    'Pref_Autofill': {'vi': 'Tự động điền và mật khẩu', 'en': 'Autofill and passwords', 'zh': '自动填充和密码'},
    'Pref_AutofillDesc': {'vi': 'Quản lý dữ liệu lưu trữ biểu mẫu', 'en': 'Manage form storage data', 'zh': '管理表单存储数据'},
    'Pref_ManagePasswords': {'vi': 'Quản lý mật khẩu', 'en': 'Manage passwords', 'zh': '管理密码'},
    'Pref_PasswordManager': {'vi': 'Trình quản lý mật khẩu', 'en': 'Password Manager', 'zh': '密码管理器'},
    'Pref_PasswordDesc': {'vi': 'Xem và sửa mật khẩu đã lưu', 'en': 'View and edit saved passwords', 'zh': '查看和编辑已保存的密码'},
    'Pref_ManagePayments': {'vi': 'Quản lý thanh toán', 'en': 'Manage payments', 'zh': '管理付款方式'},
    'Pref_PaymentMethods': {'vi': 'Phương thức thanh toán', 'en': 'Payment methods', 'zh': '付款方式'},
    'Pref_PaymentDesc': {'vi': 'Lưu số thẻ tín dụng an toàn', 'en': 'Save credit card numbers securely', 'zh': '安全保存信用卡号'},
    'Pref_ManageAddresses': {'vi': 'Quản lý địa chỉ', 'en': 'Manage addresses', 'zh': '管理地址'},
    'Pref_AddressAndMore': {'vi': 'Địa chỉ và hơn thế nữa', 'en': 'Addresses and more', 'zh': '地址及其他'},
    'Pref_AddressDesc': {'vi': 'Lưu số điện thoại, email, địa chỉ giao hàng', 'en': 'Save phone numbers, emails, shipping addresses', 'zh': '保存电话号码、电子邮件和送货地址'},

    'Pref_DefaultBrowser': {'vi': 'Trình duyệt mặc định', 'en': 'Default browser', 'zh': '默认浏览器'},
    'Pref_MakeDefault': {'vi': 'Đặt Heco làm trình duyệt mặc định', 'en': 'Make Heco the default browser', 'zh': '将 Heco 设为默认浏览器'},
    'Pref_SetDefault': {'vi': 'Đặt làm mặc định', 'en': 'Make default', 'zh': '设为默认'},
    'Pref_NotDefault': {'vi': 'Heco hiện không phải là trình duyệt mặc định của bạn', 'en': 'Heco is not currently your default browser', 'zh': 'Heco 目前不是您的默认浏览器'},

    'Pref_Privacy': {'vi': 'Quyền riêng tư & bảo mật', 'en': 'Privacy and security', 'zh': '隐私与安全'},
    'Pref_Appearance': {'vi': 'Giao diện', 'en': 'Appearance', 'zh': '外观'},
    'Pref_Search': {'vi': 'Tìm kiếm', 'en': 'Search engine', 'zh': '搜索引擎'},
    'Pref_Downloads': {'vi': 'Tải xuống', 'en': 'Downloads', 'zh': '下载'},
    'Pref_Languages': {'vi': 'Ngôn ngữ', 'en': 'Languages', 'zh': '语言'},
    'Pref_System': {'vi': 'Hệ thống', 'en': 'System', 'zh': '系统'},
    'Pref_About': {'vi': 'Giới thiệu', 'en': 'About Heco', 'zh': '关于 Heco'},
    'Pref_SearchPlaceholder': {'vi': 'Tìm kiếm...', 'en': 'Search...', 'zh': '搜索...'},
}

def append_to_lng(filename, lang_key):
    path = os.path.join(r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\language", filename)
    with open(path, 'a', encoding='utf-8') as f:
        f.write("\n\n[General]\n")
        for key, vals in translations.items():
            # to make parsing easier, we prefix with General_ in the files if we didn't specify a section in C# code
            # Actually, my C# code replaced things directly to LanguageManager.Instance["Pref_General"]
            # But the C# parser expects keys in the format Section_Key. So Pref_General means section=Pref, key=General.
            # I can just write them all under [App] and update the C# to use App_Pref_General?
            # Wait, the parser uses currentSection_key.
            # My update_xaml.py and update_cs.py used [Menu_History] - the parser will look for 'Menu_History' literally if I don't use sections?
            # Wait! The parser in LanguageManager:
            # string fullKey = $"{currentSection}_{key}";
            # So if I wrote LanguageManager.Instance["Pref_General"]
            # It expects [Pref] -> General=...
            # Let's group them by the prefix!
            pass

def generate_ini_content(lang_key):
    # Group by prefix
    groups = {}
    for key, vals in translations.items():
        if '_' in key:
            section, item_key = key.split('_', 1)
        else:
            section, item_key = 'Common', key
            
        if section not in groups:
            groups[section] = []
        groups[section].append((item_key, vals[lang_key]))
        
    content = ""
    for section, items in groups.items():
        content += f"\n[{section}]\n"
        for k, v in items:
            content += f"{k}={v}\n"
    return content

files = {
    'vi-VN.lng': 'vi',
    'en-US.lng': 'en',
    'zh-CN.lng': 'zh'
}

for filename, lang_key in files.items():
    path = os.path.join(r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\language", filename)
    content = generate_ini_content(lang_key)
    with open(path, 'a', encoding='utf-8') as f:
        f.write(content)
