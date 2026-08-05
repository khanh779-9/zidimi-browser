import os
import re

translations = {
    # BookmarksView.xaml
    '"Bấm vào ngôi sao trên thanh địa chỉ để lưu trang."': ('Bookmarks_EmptyDesc', 'Bấm vào ngôi sao trên thanh địa chỉ để lưu trang.', 'Click the star on the address bar to save the page.', '点击地址栏上的星号保存页面。'),
    '"Chưa có bookmark"': ('Bookmarks_Empty', 'Chưa có bookmark', 'No bookmarks yet', '暂无书签'),
    '"Trang bạn đã lưu để xem sau"': ('Bookmarks_ViewLater', 'Trang bạn đã lưu để xem sau', 'Pages you saved for later', '您保存以供稍后查看的页面'),
    '"Tìm bookmark"': ('Bookmarks_Search', 'Tìm bookmark', 'Search bookmarks', '搜索书签'),
    '"Xoá bookmark"': ('Bookmarks_Delete', 'Xoá bookmark', 'Delete bookmark', '删除书签'),
    
    # BrowserView.xaml
    '"Tiếp (Enter)"': ('Browser_Next', 'Tiếp (Enter)', 'Next (Enter)', '下一步 (Enter)'),
    '"Trước (Shift+Enter)"': ('Browser_Prev', 'Trước (Shift+Enter)', 'Previous (Shift+Enter)', '上一步 (Shift+Enter)'),
    
    # BrowserView.xaml.cs
    '"Bỏ bookmark"': ('Browser_RemoveBookmark', 'Bỏ bookmark', 'Remove bookmark', '删除书签'),
    '"Cài đặt trang web — sẽ triển khai ở bản sau."': ('Browser_SiteSettingsWIP', 'Cài đặt trang web — sẽ triển khai ở bản sau.', 'Site settings — to be implemented in a future release.', '网站设置 — 将在以后的版本中实现。'),
    '"Dừng tải (Esc)"': ('Browser_StopLoad', 'Dừng tải (Esc)', 'Stop loading (Esc)', '停止加载 (Esc)'),
    '"Hỏi (mặc định)"': ('Browser_AskDefault', 'Hỏi (mặc định)', 'Ask (default)', '询问（默认）'),
    '"Hồ sơ mặc định"': ('Browser_DefaultProfile', 'Hồ sơ mặc định', 'Default profile', '默认配置文件'),
    '"Khách"': ('Browser_Guest', 'Khách', 'Guest', '访客'),
    '"Không an toàn"': ('Browser_NotSecure', 'Không an toàn', 'Not secure', '不安全'),
    '"Không an toàn — trang HTTP không mã hoá"': ('Browser_NotSecureHttp', 'Không an toàn — trang HTTP không mã hoá', 'Not secure — unencrypted HTTP page', '不安全 — 未加密的 HTTP 页面'),
    '"Không lưu dữ liệu"': ('Browser_NoDataSaved', 'Không lưu dữ liệu', 'No data saved', '未保存数据'),
    '"Kết nối an toàn"': ('Browser_SecureConn', 'Kết nối an toàn', 'Secure connection', '安全连接'),
    '"Kết nối an toàn (HTTPS)"': ('Browser_SecureConnHttps', 'Kết nối an toàn (HTTPS)', 'Secure connection (HTTPS)', '安全连接 (HTTPS)'),
    '"Kết nối của bạn với trang này được mã hoá"': ('Browser_EncryptedConn', 'Kết nối của bạn với trang này được mã hoá', 'Your connection to this site is encrypted', '您与此网站的连接已加密'),
    '"Kết nối không được mã hoá — kẻ tấn công có thể xem/thay đổi thông tin"': ('Browser_UnencryptedConn', 'Kết nối không được mã hoá — kẻ tấn công có thể xem/thay đổi thông tin', 'Unencrypted connection — attackers might be able to see or change information', '未加密连接 — 攻击者可能会看到或更改信息'),
    '"Lưu trang"': ('Browser_SavePage', 'Lưu trang', 'Save page', '保存页面'),
    '"Lịch sử"': ('Browser_History', 'Lịch sử', 'History', '历史记录'),
    '"Quản lý cookie & dữ liệu trang web — sẽ triển khai ở bản sau."': ('Browser_CookieWIP', 'Quản lý cookie & dữ liệu trang web — sẽ triển khai ở bản sau.', 'Cookie & site data management — to be implemented later.', 'Cookie 及网站数据管理 — 将在以后实现。'),
    '"Thông tin trang"': ('Browser_SiteInfo', 'Thông tin trang', 'Site information', '网站信息'),
    '"Trang nội bộ"': ('Browser_InternalPage', 'Trang nội bộ', 'Internal page', '内部页面'),
    '"Trang trình duyệt cục bộ"': ('Browser_LocalBrowserPage', 'Trang trình duyệt cục bộ', 'Local browser page', '本地浏览器页面'),
    '"Tìm kiếm"': ('Browser_Search', 'Tìm kiếm', 'Search', '搜索'),
    '"Tìm kiếm: {query}"': ('Browser_SearchQuery', 'Tìm kiếm: {query}', 'Search: {query}', '搜索：{query}'),
    '"Tìm trên {engine}"': ('Browser_SearchOnEngine', 'Tìm trên {engine}', 'Search on {engine}', '在 {engine} 上搜索'),
    '"Tải lại (F5)"': ('Browser_Reload', 'Tải lại (F5)', 'Reload (F5)', '重新加载 (F5)'),
    
    # ClearDataWindow.xaml
    '"1 giờ qua"': ('Clear_1Hour', '1 giờ qua', 'Last hour', '过去一小时'),
    '"24 giờ qua"': ('Clear_24Hours', '24 giờ qua', 'Last 24 hours', '过去 24 小时'),
    '"7 ngày qua"': ('Clear_7Days', '7 ngày qua', 'Last 7 days', '过去 7 天'),
    '"Cookie và các dữ liệu trang web khác"': ('Clear_CookiesData', 'Cookie và các dữ liệu trang web khác', 'Cookies and other site data', 'Cookie 及其他网站数据'),
    '"Huỷ"': ('Clear_Cancel', 'Huỷ', 'Cancel', '取消'),
    '"Lịch sử duyệt web"': ('Clear_BrowsingHistory', 'Lịch sử duyệt web', 'Browsing history', '浏览历史记录'),
    '"Tệp và hình ảnh được lưu trong bộ nhớ đệm"': ('Clear_CachedImages', 'Tệp và hình ảnh được lưu trong bộ nhớ đệm', 'Cached images and files', '缓存的图像和文件'),
    '"Từ trước đến nay"': ('Clear_AllTime', 'Từ trước đến nay', 'All time', '所有时间'),
    '"Xoá dữ liệu"': ('Clear_ClearData', 'Xoá dữ liệu', 'Clear data', '清除数据'),
    '"Xoá dữ liệu duyệt web"': ('Clear_ClearBrowsingData', 'Xoá dữ liệu duyệt web', 'Clear browsing data', '清除浏览数据'),
    
    # ClearDataWindow.xaml.cs
    '"Có lỗi xảy ra: {ex.Message}"': ('Clear_ErrorMsg', 'Có lỗi xảy ra: {ex.Message}', 'An error occurred: {ex.Message}', '发生错误：{ex.Message}'),
    '"Dữ liệu duyệt web đã được xoá."': ('Clear_DataCleared', 'Dữ liệu duyệt web đã được xoá.', 'Browsing data cleared.', '浏览数据已清除。'),
    '"Lỗi"': ('Clear_Error', 'Lỗi', 'Error', '错误'),
    '"Thành công"': ('Clear_Success', 'Thành công', 'Success', '成功'),
    '"Đang xoá..."': ('Clear_Clearing', 'Đang xoá...', 'Clearing...', '正在清除...'),
    
    # DataManagerWindow.xaml
    '"Lưu"': ('DataMgr_Save', 'Lưu', 'Save', '保存'),
    '"Quản lý dữ liệu"': ('DataMgr_ManageData', 'Quản lý dữ liệu', 'Manage data', '管理数据'),
    '"Xoá"': ('DataMgr_Delete', 'Xoá', 'Delete', '删除'),
    '"Đóng"': ('DataMgr_Close', 'Đóng', 'Close', '关闭'),
    
    # DataManagerWindow.xaml.cs
    '"Họ và tên"': ('DataMgr_FullName', 'Họ và tên', 'Full name', '全名'),
    '"Mật khẩu"': ('DataMgr_Password', 'Mật khẩu', 'Password', '密码'),
    '"Ngày hết hạn (MM/YY)"': ('DataMgr_ExpiryDate', 'Ngày hết hạn (MM/YY)', 'Expiry date (MM/YY)', '到期日期 (MM/YY)'),
    '"Quản lý mật khẩu"': ('DataMgr_ManagePasswords', 'Quản lý mật khẩu', 'Manage passwords', '管理密码'),
    '"Quản lý thẻ thanh toán"': ('DataMgr_ManageCards', 'Quản lý thẻ thanh toán', 'Manage payment cards', '管理付款卡'),
    '"Quản lý địa chỉ"': ('DataMgr_ManageAddresses', 'Quản lý địa chỉ', 'Manage addresses', '管理地址'),
    '"Số thẻ"': ('DataMgr_CardNumber', 'Số thẻ', 'Card number', '卡号'),
    '"Số điện thoại"': ('DataMgr_Phone', 'Số điện thoại', 'Phone number', '电话号码'),
    '"Tên trên thẻ"': ('DataMgr_NameOnCard', 'Tên trên thẻ', 'Name on card', '卡上的姓名'),
    '"Tên đăng nhập"': ('DataMgr_Username', 'Tên đăng nhập', 'Username', '用户名'),
    '"Địa chỉ chi tiết"': ('DataMgr_AddressDetail', 'Địa chỉ chi tiết', 'Detailed address', '详细地址'),
    
    # DownloadsView.xaml
    '"Mở thư mục"': ('Downloads_OpenFolder', 'Mở thư mục', 'Open folder', '打开文件夹'),
    '"Tìm file tải"': ('Downloads_Search', 'Tìm file tải', 'Search downloads', '搜索下载'),
    '"Xoá khỏi danh sách"': ('Downloads_RemoveFromList', 'Xoá khỏi danh sách', 'Remove from list', '从列表中删除'),
    '"Đang tải..."': ('Downloads_Downloading', 'Đang tải...', 'Downloading...', '正在下载...'),
    
    # HistoryView.xaml
    '"Mở lại"': ('History_Reopen', 'Mở lại', 'Reopen', '重新打开'),
    '"Tìm theo tên trang hoặc URL"': ('History_Search', 'Tìm theo tên trang hoặc URL', 'Search by page name or URL', '按页面名称或 URL 搜索'),
    '"Xoá"': ('History_Delete', 'Xoá', 'Delete', '删除'),
    
    # LoginWindow.xaml
    '"Đóng"': ('Login_Close', 'Đóng', 'Close', '关闭'),
    '"Đăng nhập"': ('Login_SignIn', 'Đăng nhập', 'Sign in', '登录'),
    '"Đăng nhập Heco"': ('Login_SignInHeco', 'Đăng nhập Heco', 'Sign in to Heco', '登录 Heco'),
    
    # LoginWindow.xaml.cs
    '"Email không hợp lệ."': ('Login_InvalidEmail', 'Email không hợp lệ.', 'Invalid email.', '无效的电子邮件。'),
    '"Vui lòng nhập đầy đủ Email và Mật khẩu."': ('Login_EmptyFields', 'Vui lòng nhập đầy đủ Email và Mật khẩu.', 'Please enter both Email and Password.', '请输入电子邮件和密码。'),
    '"Đang xử lý..."': ('Login_Processing', 'Đang xử lý...', 'Processing...', '正在处理...'),
    '"Đăng nhập thành công với tài khoản {TxtEmail.Text}!"': ('Login_SuccessMsg', 'Đăng nhập thành công với tài khoản {TxtEmail.Text}!', 'Successfully signed in as {TxtEmail.Text}!', '已成功登录账号 {TxtEmail.Text}！'),
    '"Đồng bộ"': ('Login_Sync', 'Đồng bộ', 'Sync', '同步'),
    
    # PreferencesView.xaml.cs
    '"Bạn có chắc chắn muốn đăng xuất và ngừng đồng bộ?"': ('Pref_ConfirmLogout', 'Bạn có chắc chắn muốn đăng xuất và ngừng đồng bộ?', 'Are you sure you want to sign out and stop syncing?', '您确定要退出并停止同步吗？'),
    '"Bảo vệ dữ liệu cá nhân"': ('Pref_ProtectData', 'Bảo vệ dữ liệu cá nhân', 'Protect personal data', '保护个人数据'),
    '"Chạy nền khi đóng cửa sổ trình duyệt"': ('Pref_RunInBackground', 'Chạy nền khi đóng cửa sổ trình duyệt', 'Run in background when closed', '关闭时在后台运行'),
    '"Chặn cookie bên thứ ba"': ('Pref_BlockThirdPartyCookies', 'Chặn cookie bên thứ ba', 'Block third-party cookies', '阻止第三方 Cookie'),
    '"Chọn ngôn ngữ giao diện"': ('Pref_SelectUILang', 'Chọn ngôn ngữ giao diện', 'Select UI language', '选择界面语言'),
    '"Chọn theme sáng/tối"': ('Pref_SelectTheme', 'Chọn theme sáng/tối', 'Select light/dark theme', '选择浅色/深色主题'),
    '"Chủ đề"': ('Pref_Theme', 'Chủ đề', 'Theme', '主题'),
    '"Cài đặt công cụ tìm kiếm"': ('Pref_SearchSettings', 'Cài đặt công cụ tìm kiếm', 'Search engine settings', '搜索引擎设置'),
    '"Cài đặt hệ thống & hiệu suất"': ('Pref_SystemSettings', 'Cài đặt hệ thống & hiệu suất', 'System & performance settings', '系统和性能设置'),
    '"Cá nhân"': ('Pref_PersonalProfile', 'Cá nhân', 'Personal', '个人'),
    '"Cảnh báo trang web nguy hiểm"': ('Pref_WarnDangerousSites', 'Cảnh báo trang web nguy hiểm', 'Warn about dangerous sites', '危险网站警告'),
    '"Cập nhật"': ('Pref_Update', 'Cập nhật', 'Update', '更新'),
    '"Cỡ chữ"': ('Pref_FontSize', 'Cỡ chữ', 'Font size', '字体大小'),
    '"Engine tìm kiếm"': ('Pref_SearchEngineTitle', 'Engine tìm kiếm', 'Search engine', '搜索引擎'),
    '"Giấy phép"': ('Pref_License', 'Giấy phép', 'License', '许可证'),
    '"Gửi yêu cầu \\"Không theo dõi\\" (Do Not Track)"': ('Pref_DoNotTrack', 'Gửi yêu cầu "Không theo dõi" (Do Not Track)', 'Send a "Do Not Track" request', '发送“不跟踪”(Do Not Track)请求'),
    '"Heco Browser đã được cập nhật phiên bản mới nhất."': ('Pref_UpToDate', 'Heco Browser đã được cập nhật phiên bản mới nhất.', 'Heco Browser is up to date.', 'Heco 浏览器已是最新版本。'),
    '"Hiển thị gợi ý tìm kiếm khi gõ"': ('Pref_ShowSearchSuggestions', 'Hiển thị gợi ý tìm kiếm khi gõ', 'Show search suggestions as you type', '在您键入时显示搜索建议'),
    '"Hiện thanh tải xuống khi bắt đầu tải"': ('Pref_ShowDownloadBar', 'Hiện thanh tải xuống khi bắt đầu tải', 'Show download bar when download starts', '开始下载时显示下载栏'),
    '"Hệ thống"': ('Pref_SystemTitle', 'Hệ thống', 'System', '系统'),
    '"Hỏi nơi lưu trước khi tải"': ('Pref_AskWhereToSave', 'Hỏi nơi lưu trước khi tải', 'Ask where to save each file before downloading', '下载前询问每个文件的保存位置'),
    # Use format strings for profile count
    '"Hồ sơ {AppSettings.Current.Profiles.Count + 1}"': ('Pref_ProfileCount', 'Hồ sơ {AppSettings.Current.Profiles.Count + 1}', 'Profile {AppSettings.Current.Profiles.Count + 1}', '配置文件 {AppSettings.Current.Profiles.Count + 1}'),
    '"Không thể mở Cài đặt Proxy Windows: "': ('Pref_ProxyError', 'Không thể mở Cài đặt Proxy Windows: ', 'Could not open Windows Proxy Settings: ', '无法打开 Windows 代理设置：'),
    '"Không thể mở Cài đặt Windows: "': ('Pref_WinSettingsError', 'Không thể mở Cài đặt Windows: ', 'Could not open Windows Settings: ', '无法打开 Windows 设置：'),
    '"Kiểm tra an toàn trang web (Safe Browsing)"': ('Pref_SafeBrowsing', 'Kiểm tra an toàn trang web (Safe Browsing)', 'Safe Browsing', '安全浏览'),
    '"Kiểm tra cập nhật"': ('Pref_CheckUpdate', 'Kiểm tra cập nhật', 'Check for updates', '检查更新'),
    '"Kích thước chữ mặc định"': ('Pref_DefaultFontSize', 'Kích thước chữ mặc định', 'Default font size', '默认字体大小'),
    '"Lỗi"': ('Pref_Error', 'Lỗi', 'Error', '错误'),
    '"Lớn (16px)"': ('Pref_SizeLarge', 'Lớn (16px)', 'Large (16px)', '大 (16px)'),
    '"Mã nguồn"': ('Pref_SourceCode', 'Mã nguồn', 'Source code', '源代码'),
    '"Mở cài đặt proxy Windows"': ('Pref_OpenProxySettings', 'Mở cài đặt proxy Windows', 'Open Windows proxy settings', '打开 Windows 代理设置'),
    '"Mở thư mục"': ('Pref_OpenFolder', 'Mở thư mục', 'Open folder', '打开文件夹'),
    '"Ngôn ngữ hiển thị"': ('Pref_DisplayLang', 'Ngôn ngữ hiển thị', 'Display language', '显示语言'),
    '"Ngôn ngữ hiển thị & dịch trang"': ('Pref_LangTitle', 'Ngôn ngữ hiển thị & dịch trang', 'Display language & translation', '显示语言和翻译'),
    '"Nhỏ (12px)"': ('Pref_SizeSmall', 'Nhỏ (12px)', 'Small (12px)', '小 (12px)'),
    '"Phiên bản"': ('Pref_Version', 'Phiên bản', 'Version', '版本'),
    '"Quản lý file tải xuống"': ('Pref_ManageDownloads', 'Quản lý file tải xuống', 'Manage downloaded files', '管理下载的文件'),
    '"Rất lớn (18px)"': ('Pref_SizeExtraLarge', 'Rất lớn (18px)', 'Extra large (18px)', '特大 (18px)'),
    '"Sáng"': ('Pref_ThemeLight', 'Sáng', 'Light', '浅色'),
    '"Sử dụng proxy hệ thống"': ('Pref_UseSystemProxy', 'Sử dụng proxy hệ thống', 'Use system proxy settings', '使用系统代理设置'),
    '"Sử dụng tăng tốc phần cứng (GPU) khi khả dụng"': ('Pref_HardwareAccel', 'Sử dụng tăng tốc phần cứng (GPU) khi khả dụng', 'Use hardware acceleration when available', '可用时使用硬件加速'),
    '"Thông tin phiên bản & bản quyền"': ('Pref_VersionInfo', 'Thông tin phiên bản & bản quyền', 'Version & copyright info', '版本和版权信息'),
    '"Thư mục tải xuống mặc định"': ('Pref_DefaultDownloadFolder', 'Thư mục tải xuống mặc định', 'Default download folder', '默认下载文件夹'),
    '"Tiếng Việt"': ('Pref_LangVietnamese', 'Tiếng Việt', 'Vietnamese', '越南语'),
    '"Tuỳ chỉnh giao diện trình duyệt"': ('Pref_CustomizeAppearance', 'Tuỳ chỉnh giao diện trình duyệt', 'Customize browser appearance', '自定义浏览器外观'),
    '"Tối"': ('Pref_ThemeDark', 'Tối', 'Dark', '深色'),
    '"Tự động dịch trang web không phải tiếng Việt"': ('Pref_AutoTranslate', 'Tự động dịch trang web không phải tiếng Việt', 'Automatically translate pages not in your language', '自动翻译非您的语言的网页'),
    '"Vừa (14px)"': ('Pref_SizeMedium', 'Vừa (14px)', 'Medium (14px)', '中 (14px)'),
    '"Xoá dữ liệu duyệt web..."': ('Pref_ClearBrowsingDataBtn', 'Xoá dữ liệu duyệt web...', 'Clear browsing data...', '清除浏览数据...'),
    '"Zoom mặc định cho mọi trang"': ('Pref_DefaultZoom', 'Zoom mặc định cho mọi trang', 'Default zoom for all pages', '所有页面的默认缩放'),
    '"Đang kiểm tra..."': ('Pref_CheckingUpdate', 'Đang kiểm tra...', 'Checking...', '正在检查...'),
    
    # TabStrip.xaml
    '"Tab mới (Ctrl+T)"': ('Tab_NewTabShortcut', 'Tab mới (Ctrl+T)', 'New tab (Ctrl+T)', '新标签页 (Ctrl+T)'),
    
    # TabStrip.xaml.cs
    '"Bật tiếng tab"': ('Tab_UnmuteTab', 'Bật tiếng tab', 'Unmute tab', '取消标签页静音'),
    '"Bỏ ghim tab"': ('Tab_UnpinTab', 'Bỏ ghim tab', 'Unpin tab', '取消固定标签页'),
    '"Ghim tab"': ('Tab_PinTab', 'Ghim tab', 'Pin tab', '固定标签页'),
    '"Nhân bản tab"': ('Tab_DuplicateTab', 'Nhân bản tab', 'Duplicate tab', '复制标签页'),
    '"Tab mới"': ('Tab_NewTab', 'Tab mới', 'New tab', '新标签页'),
    '"Tải lại tab"': ('Tab_ReloadTab', 'Tải lại tab', 'Reload tab', '重新加载标签页'),
    '"Tắt tiếng tab"': ('Tab_MuteTab', 'Tắt tiếng tab', 'Mute tab', '标签页静音'),
    '"Đóng tab"': ('Tab_CloseTab', 'Đóng tab', 'Close tab', '关闭标签页')
}

# 1. Update the .lng files
def update_lng_files():
    base_dir = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\language"
    files = {'vi-VN.lng': 1, 'en-US.lng': 2, 'zh-CN.lng': 3}
    
    for filename, idx in files.items():
        path = os.path.join(base_dir, filename)
        with open(path, 'a', encoding='utf-8') as f:
            for literal, data in translations.items():
                key = data[0]
                val = data[idx]
                f.write(f"{key}={val}\n")
                
# 2. Update .cs and .xaml files
def update_source_files():
    base_dir = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\Views"
    
    for root, _, files in os.walk(base_dir):
        for f in files:
            if f.endswith('.cs') or f.endswith('.xaml'):
                path = os.path.join(root, f)
                with open(path, 'r', encoding='utf-8') as file:
                    content = file.read()
                
                original_content = content
                for literal, data in translations.items():
                    key = data[0]
                    # If it's a .cs file
                    if f.endswith('.cs'):
                        # special cases for string interpolation
                        if literal == '"Hồ sơ {AppSettings.Current.Profiles.Count + 1}"':
                            content = content.replace(f'${literal}', f'string.Format(LanguageManager.Instance["{key}"], AppSettings.Current.Profiles.Count + 1)')
                        elif literal == '"Có lỗi xảy ra: {ex.Message}"':
                            content = content.replace(f'${literal}', f'string.Format(LanguageManager.Instance["{key}"], ex.Message)')
                        elif literal == '"Đăng nhập thành công với tài khoản {TxtEmail.Text}!"':
                            content = content.replace(f'${literal}', f'string.Format(LanguageManager.Instance["{key}"], TxtEmail.Text)')
                        elif literal == '"Gửi yêu cầu \\"Không theo dõi\\" (Do Not Track)"':
                            # it might be escaped
                            content = content.replace(r'"Gửi yêu cầu \"Không theo dõi\" (Do Not Track)"', f'LanguageManager.Instance["{key}"]')
                        else:
                            content = content.replace(literal, f'LanguageManager.Instance["{key}"]')
                    
                    # If it's a .xaml file
                    elif f.endswith('.xaml'):
                        # literal includes double quotes, e.g. '"Text"'
                        inner_text = literal.strip('"')
                        
                        # common attributes in XAML
                        attrs = ['Text', 'ToolTip', 'Content', 'Header', 'Title']
                        for attr in attrs:
                            search_str = f'{attr}="{inner_text}"'
                            replace_str = f'{attr}="{{Binding Source={{x:Static infra:LanguageManager.Instance}}, Path=[{key}]}}"'
                            content = content.replace(search_str, replace_str)
                            
                if content != original_content:
                    if f.endswith('.xaml') and 'xmlns:infra' not in content:
                        content = content.replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n             xmlns:infra="clr-namespace:Heco.Browser.Infrastructure"')
                    with open(path, 'w', encoding='utf-8') as file:
                        file.write(content)

if __name__ == '__main__':
    update_lng_files()
    update_source_files()
