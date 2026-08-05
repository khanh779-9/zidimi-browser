import os

replacements = {
    '"Chung"': 'LanguageManager.Instance["Pref_General"]',
    '"Cài đặt chung cho trình duyệt"': 'LanguageManager.Instance["Pref_GeneralDesc"]',
    '"Trang khi mở tab mới"': 'LanguageManager.Instance["Pref_StartupPage"]',
    '"URL trang chủ"': 'LanguageManager.Instance["Pref_HomeUrl"]',
    '"Công cụ tìm kiếm mặc định"': 'LanguageManager.Instance["Pref_DefaultEngine"]',
    '"Chọn engine tìm kiếm"': 'LanguageManager.Instance["Pref_SelectEngine"]',
    '"Khi khởi động"': 'LanguageManager.Instance["Pref_OnStartup"]',
    '"Chọn hành động khởi động"': 'LanguageManager.Instance["Pref_StartupAction"]',
    '"Mở trang mới"': 'LanguageManager.Instance["Pref_StartupNewPage"]',
    '"Tiếp tục từ nơi đã dừng"': 'LanguageManager.Instance["Pref_StartupContinue"]',
    '"Mở tập trang cụ thể"': 'LanguageManager.Instance["Pref_StartupSpecific"]',

    '"Bạn và Heco"': 'LanguageManager.Instance["Pref_Profile"]',
    '"Quản lý hồ sơ và đồng bộ hoá"': 'LanguageManager.Instance["Pref_ProfileDesc"]',
    '"Đăng xuất"': 'LanguageManager.Instance["Pref_Logout"]',
    '"Đăng nhập"': 'LanguageManager.Instance["Pref_Login"]',
    '"Đồng bộ dữ liệu"': 'LanguageManager.Instance["Pref_SyncData"]',
    '"Đăng nhập để đồng bộ lịch sử, dấu trang và mật khẩu"': 'LanguageManager.Instance["Pref_SyncDesc"]',
    '"Hồ sơ hiện tại"': 'LanguageManager.Instance["Pref_CurrentProfile"]',
    '"Đổi profile sẽ áp dụng khi mở Tab mới"': 'LanguageManager.Instance["Pref_ProfileApplyDesc"]',
    '"Thêm hồ sơ"': 'LanguageManager.Instance["Pref_AddProfile"]',

    '"Tự động điền và mật khẩu"': 'LanguageManager.Instance["Pref_Autofill"]',
    '"Quản lý dữ liệu lưu trữ biểu mẫu"': 'LanguageManager.Instance["Pref_AutofillDesc"]',
    '"Quản lý mật khẩu"': 'LanguageManager.Instance["Pref_ManagePasswords"]',
    '"Trình quản lý mật khẩu"': 'LanguageManager.Instance["Pref_PasswordManager"]',
    '"Xem và sửa mật khẩu đã lưu"': 'LanguageManager.Instance["Pref_PasswordDesc"]',
    '"Quản lý thanh toán"': 'LanguageManager.Instance["Pref_ManagePayments"]',
    '"Phương thức thanh toán"': 'LanguageManager.Instance["Pref_PaymentMethods"]',
    '"Lưu số thẻ tín dụng an toàn"': 'LanguageManager.Instance["Pref_PaymentDesc"]',
    '"Quản lý địa chỉ"': 'LanguageManager.Instance["Pref_ManageAddresses"]',
    '"Địa chỉ và hơn thế nữa"': 'LanguageManager.Instance["Pref_AddressAndMore"]',
    '"Lưu số điện thoại, email, địa chỉ giao hàng"': 'LanguageManager.Instance["Pref_AddressDesc"]',

    '"Trình duyệt mặc định"': 'LanguageManager.Instance["Pref_DefaultBrowser"]',
    '"Đặt Heco làm trình duyệt mặc định"': 'LanguageManager.Instance["Pref_MakeDefault"]',
    '"Đặt làm mặc định"': 'LanguageManager.Instance["Pref_SetDefault"]',
    '"Heco hiện không phải là trình duyệt mặc định của bạn"': 'LanguageManager.Instance["Pref_NotDefault"]',

    '"Quyền riêng tư & bảo mật"': 'LanguageManager.Instance["Pref_Privacy"]',
    '"Giao diện"': 'LanguageManager.Instance["Pref_Appearance"]',
    '"Tìm kiếm"': 'LanguageManager.Instance["Pref_Search"]',
    '"Tải xuống"': 'LanguageManager.Instance["Pref_Downloads"]',
    '"Ngôn ngữ"': 'LanguageManager.Instance["Pref_Languages"]',
    '"Hệ thống"': 'LanguageManager.Instance["Pref_System"]',
    '"Giới thiệu"': 'LanguageManager.Instance["Pref_About"]',
}

def update_cs():
    path = r"d:\Data\Tailieu\Projects\C#\Heco_Browser\Heco.Browser\Views\PreferencesView.xaml.cs"
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    for k, v in replacements.items():
        # we have to be careful with string literals, replacing exact matches
        # only replace occurrences that look like string assignments
        content = content.replace(f"Text = {k}", f"Text = {v}")
        content = content.replace(f"Content = {k}", f"Content = {v}")
        content = content.replace(f"CreateSettingRow({k}", f"CreateSettingRow({v}")
        content = content.replace(f", {k}", f", {v}")
    
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

update_cs()
