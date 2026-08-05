import os

replacements_xaml = {
    'Text="Lịch sử"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Menu_History]}"',
    'Text="Trang web bạn đã ghé thăm gần đây"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[History_Desc]}"',
    'Text="Xoá tất cả"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Common_ClearAll]}"',

    'Text="Tải xuống"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Downloads_Title]}"',
    'Text="Các file bạn đã tải"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Downloads_Desc]}"',
    'Text="Chưa có file tải nào"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Downloads_Empty]}"',
    'Text="Các file bạn tải sẽ xuất hiện ở đây."': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Downloads_EmptyDesc]}"',

    'Text="Bookmark"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Bookmarks_Title]}"',
    'Text="Các trang bạn đã lưu"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Bookmarks_Desc]}"',

    'ToolTip="Cuộn sang trái"': 'ToolTip="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Tab_ScrollLeft]}"',
    'ToolTip="Tắt/Bật tiếng"': 'ToolTip="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Tab_Mute]}"',
    'ToolTip="Cuộn sang phải"': 'ToolTip="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Tab_ScrollRight]}"',
    'ToolTip="Danh sách tab"': 'ToolTip="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Tab_List]}"',

    'Text="Đăng nhập để đồng bộ"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Login_Title]}"',
    'Text="Lịch sử, mật khẩu và dấu trang của bạn sẽ được an toàn."': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Login_Desc]}"',
    'Text="Email"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Login_Email]}"',
    'Text="Mật khẩu"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Login_Password]}"',

    'Text="Xoá dữ liệu duyệt web"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Clear_Title]}"',
    'Text="Phạm vi thời gian:"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Clear_TimeRange]}"',
    'Text="Xoá lịch sử duyệt web từ thiết bị này."': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Clear_HistoryDesc]}"',
    'Text="Đăng xuất bạn khỏi hầu hết các trang web."': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Clear_CookiesDesc]}"',
    'Text="Giải phóng không gian lưu trữ."': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Clear_CacheDesc]}"',

    'Text="Quản lý dữ liệu"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Data_Title]}"',
    'Text="Thêm mới"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Data_Add]}"',

    'Text="Chung"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_General]}"',
    'Text="Bạn và Heco"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Profile]}"',
    'Text="Tự động điền và mật khẩu"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Autofill]}"',
    'Text="Trình duyệt mặc định"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_DefaultBrowser]}"',
    'Text="Quyền riêng tư &amp; bảo mật"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Privacy]}"',
    'Text="Giao diện"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Appearance]}"',
    'Text="Tìm kiếm"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Search]}"',
    'Text="Ngôn ngữ"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_Languages]}"',
    'Text="Hệ thống"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_System]}"',
    'Text="Giới thiệu"': 'Text="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_About]}"',
    'ToolTip="Tìm kiếm..."': 'ToolTip="{Binding Source={x:Static infra:LanguageManager.Instance}, Path=[Pref_SearchPlaceholder]}"',
}

def update_xaml():
    base_dir = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\Views"
    for file in os.listdir(base_dir):
        if file.endswith('.xaml'):
            path = os.path.join(base_dir, file)
            with open(path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            for k, v in replacements_xaml.items():
                content = content.replace(k, v)
            
            # ensure xmlns:infra is there if we replaced something
            if content != original_content and 'xmlns:infra' not in content:
                content = content.replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n             xmlns:infra="clr-namespace:Heco.Browser.Infrastructure"')
            
            with open(path, 'w', encoding='utf-8') as f:
                f.write(content)

update_xaml()
