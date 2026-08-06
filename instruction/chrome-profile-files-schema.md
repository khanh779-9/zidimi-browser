# Schema các file trong thư mục `User Data\<Profile>` của Chrome/Chromium

> Ghi chú chung: Hầu hết các file này **là SQLite 3** (mở bằng DB Browser for SQLite, `sqlite3 file.db ".schema"`...), nhưng **không phải tất cả**. Một số file là JSON, Protobuf, hoặc định dạng nhị phân riêng (không phải SQLite). Mình ghi rõ định dạng ở đầu mỗi mục. Với các file quá mới/ít tài liệu công khai, mình ghi chú "chưa đủ nguồn xác thực" thay vì đoán bừa cột.

---

## 1. File: `Login Data` (SQLite)
Lưu thông tin đăng nhập đã lưu. Mật khẩu (`password_value`) là BLOB đã **mã hoá** (DPAPI trên Windows / Keychain trên macOS / libsecret trên Linux), không phải plaintext.

a. **logins**(origin_url, action_url, username_element, username_value, password_element, password_value, submit_element, signon_realm, date_created, blacklisted_by_user, scheme, password_type, times_used, form_data, date_synced, display_name, icon_url, federation_url, skip_zero_click, generation_upload_status, possible_username_pairs, id, date_last_used, moving_blocked_for, date_password_modified)

b. **stats**(username_value, origin_domain, used_username_counter, dismissal_count, update_time)

c. **insecure_credentials**(parent_id, insecurity_type, create_time, is_muted)

d. **field_info**(form_signature, field_signature, field_type, create_time)

e. **sync_entities_metadata / sync_model_metadata** (dữ liệu metadata phục vụ Chrome Sync)

f. **meta**(key, value) — version, last_compatible_version...

> `Login Data For Account` có cấu trúc tương tự `Login Data` nhưng dùng cho các tài khoản đã đăng nhập vào trình duyệt (account-scoped storage) khi bật tính năng "account storage".

---

## 2. File: `Web Data` (SQLite)
Lưu dữ liệu AutoFill: form cũ, địa chỉ, thẻ thanh toán, công cụ tìm kiếm...

a. **autofill**(name, value, value_lower, date_created, date_last_used, count) — dữ liệu form cũ (kiểu name/value)

b. **autofill_profiles**(guid, company_name, street_address, dependent_locality, city, state, zipcode, sorting_code, country_code, date_modified, origin, language_code, use_count, use_date, label, disallow_settings_visible_updates)

c. **autofill_profile_names**(guid, first_name, middle_name, last_name, full_name, ...)

d. **autofill_profile_emails**(guid, email)

e. **autofill_profile_phones**(guid, number)

f. **credit_cards**(guid, name_on_card, expiration_month, expiration_year, card_number_encrypted, date_modified, origin, use_count, use_date, billing_address_id, nickname)

g. **keywords**(id, short_name, keyword, favicon_url, url, safe_for_autoreplace, originating_url, date_created, usage_count, input_encodings, suggest_url, prepopulate_id, created_by_policy, last_modified, sync_guid, alternate_urls, image_url, search_url_post_params, suggest_url_post_params, image_url_post_params, new_tab_url, last_visited, ...) — danh sách công cụ tìm kiếm

h. **token_service**(service, encrypted_token) — token OAuth dịch vụ đã lưu (mã hoá)

i. **web_apps**(url, has_all_images)

j. **masked_credit_cards**, **server_addresses**, **payments_customer_data**... (dữ liệu thẻ/địa chỉ đồng bộ từ server Google, tuỳ phiên bản)

k. **meta**(key, value)

> `Account Web Data` là bản sao cấu trúc tương tự nhưng scope theo tài khoản Google đăng nhập (không lưu local nếu người dùng không đồng bộ).

---

## 3. File: `History` (SQLite)
Lưu lịch sử duyệt web, tải file, từ khoá tìm kiếm.

a. **urls**(id, url, title, visit_count, typed_count, last_visit_time, hidden, favicon_id)

b. **visits**(id, url, visit_time, from_visit, transition, segment_id, visit_duration, incremented_omnibox_typed_score, opener_visit, ...)

c. **visit_source**(id, source) — nguồn của lượt visit (đồng bộ / import / local)

d. **downloads**(id, guid, current_path, target_path, start_time, received_bytes, total_bytes, state, danger_type, interrupt_reason, end_time, opened, last_access_time, transient, referrer, site_url, embedder_download_data, tab_url, tab_referrer_url, http_method, by_ext_id, by_ext_name, etag, last_modified, mime_type, original_mime_type)

e. **downloads_url_chains**(id, chain_index, url)

f. **downloads_slices**(download_id, offset, received_bytes, finished)

g. **keyword_search_terms**(keyword_id, url_id, term, normalized_term)

h. **segments**(id, name, url_id)

i. **segment_usage**(id, segment_id, time_slot, visit_count)

j. **content_annotations**, **context_annotations** (tuỳ phiên bản Chrome mới, lưu annotation nội bộ cho tính năng history/journeys)

k. **meta**(key, value)

---

## 4. File: `Favicons` (SQLite)
Lưu icon (favicon) của các trang web.

a. **favicons**(id, url, icon_type)

b. **favicon_bitmaps**(id, icon_id, last_updated, image_data, width, height, last_requested)

c. **icon_mapping**(id, page_url, icon_id)

d. **meta**(key, value)

---

## 5. File: `Top Sites` (SQLite)
Lưu danh sách trang web hay truy cập (hiển thị ở New Tab Page).

a. **top_sites**(url, url_rank, title, redirects, ...) — cấu trúc chính xác thay đổi khá nhiều theo phiên bản Chrome; nhìn chung gồm url, rank, title, thumbnail-related fields.

b. **meta**(key, value)

---

## 6. File: `Extension Cookies` (SQLite)
Cookie riêng cho các extension (context tách biệt với cookie web thường). Cấu trúc bảng **cookies** tương tự file `Cookies` bên dưới.

---

## 7. File: `Cookies` *(không có trong danh sách bạn liệt kê nhưng liên quan trực tiếp tới "Extension Cookies", ghi thêm để đối chiếu)* (SQLite)

a. **cookies**(creation_utc, host_key, top_frame_site_key, name, value, encrypted_value, path, expires_utc, is_secure, is_httponly, last_access_utc, has_expires, is_persistent, priority, samesite, source_scheme, source_port, last_update_utc, source_type, has_cross_site_ancestor)

b. **meta**(key, value)

> `value` chứa cookie dạng plaintext (thường rỗng ở bản Chrome mới), `encrypted_value` là BLOB mã hoá AES (tiền tố `v10`/`v11`...).

---

## 8. File: `MediaDeviceSalts` (SQLite)
Lưu "salt" dùng để băm ID thiết bị media (camera/mic) theo từng origin, phục vụ tính năng chống fingerprinting của WebRTC/MediaDevices.

a. **salts**(origin, salt, last_modified) *(tên cột tham khảo từ mã nguồn Chromium `media_device_salt_database.cc`; nên đối chiếu trực tiếp bằng `.schema` vì tài liệu công khai không nhiều)*

---

## 9. File: `Shortcuts` (SQLite)
Lưu lịch sử gợi ý Omnibox (những gì bạn gõ và đã chọn), phục vụ autocomplete thanh địa chỉ.

a. **omni_box_shortcuts**(id, text, fill_into_edit, url, contents, contents_class, description, description_class, transition, type, keyword, last_access_time, number_of_hits)

---

## 10. File: `Translate Ranker Model` (không phải SQLite)
Đây là file **protobuf nhị phân** (không phải database SQLite) chứa mô hình machine-learning nhỏ (ranker) mà Chrome dùng để quyết định có nên hiện gợi ý dịch trang hay không. Không có "bảng/cột" theo nghĩa SQL — cấu trúc là protobuf message (`chrome_intelligence.RankerModel` trong mã nguồn Chromium), không được tài liệu hoá công khai chi tiết.

---

## 11. File: `Preferences` (JSON, không phải SQLite)
File JSON lớn chứa cấu hình profile: search engine mặc định, danh sách extension, trạng thái bật/tắt tính năng, thông tin đồng bộ, v.v. Không có bảng/cột — có cấu trúc key/value lồng nhau kiểu JSON (`{"account_info": ..., "extensions": ..., "search_engines": ...}`).

## 12. File: `PreferredApps` (JSON, không phải SQLite)
Trên ChromeOS/Chrome, lưu ánh xạ loại nội dung/URL với ứng dụng mặc định người dùng chọn để mở. Định dạng JSON, không phải SQLite.

---

## 13. File: `Account Web Data` (SQLite)
Xem mục 2 (`Web Data`) — cùng cấu trúc bảng, nhưng dữ liệu scope theo tài khoản Google đã đăng nhập trình duyệt (dùng cho tính năng lưu autofill/thẻ theo tài khoản thay vì theo máy).

---

## 14. File: `Account Web Data`, `Affiliation Database` (SQLite)
Chrome Password Manager dùng để lưu cache thông tin "affiliation" (ánh xạ giữa các app Android và website có cùng chủ sở hữu, để gợi ý mật khẩu dùng chung).

a. **eq_classes**(id) *(tham khảo mã nguồn `affiliation_database.cc`)*

b. **eq_class_members**(id, eq_class_id, facet_uri, group_id)

c. **eq_class_groups**(id, group_display_name, group_icon_url)

> Tên cột ở trên lấy từ mã nguồn Chromium (`components/password_manager/core/browser/affiliation/affiliation_database.cc`) — nên nếu cần chính xác 100% theo bản Chrome đang dùng, hãy mở trực tiếp bằng `sqlite3 "Affiliation Database" ".schema"`.

---

## 15. File: `DIPS` (SQLite)
DIPS = "Detection of Insufficient Party Signals" / (trước đây "Bounce Tracking Mitigation"). Lưu tín hiệu tương tác người dùng theo từng site để phát hiện/giảm thiểu bounce-tracking.

a. **bounces**(site, first_site_storage_time, last_site_storage_time, first_user_interaction_time, last_user_interaction_time, first_stateful_bounce_time, last_stateful_bounce_time, first_stateless_bounce_time, last_stateless_bounce_time, ...) *(tên cột tham khảo mã nguồn `dips_database.cc`; cấu trúc thay đổi khá thường xuyên giữa các bản Chrome nên nên đối chiếu trực tiếp)*

---

## 16. File: `BrowsingTopicsSiteData`, `BrowsingTopicsState` (SQLite / hỗn hợp)
Phục vụ Privacy Sandbox – Topics API.

- `BrowsingTopicsSiteData` (SQLite): lưu dữ liệu site đã truy cập liên quan Topics API — bảng chính thường tên **browsing_topics_api_usage** hoặc tương tự (ghi lại origin, thời điểm truy cập API topics).
- `BrowsingTopicsState`: thường là file lưu **state nhị phân/proto** (danh sách "epoch topics" đã tính cho profile), không hẳn là bảng SQL thuần.

> Đây là 2 file khá mới (từ Chrome ~115+), tài liệu công khai về schema chi tiết còn rất ít — nếu cần chính xác, nên mở trực tiếp bằng SQLite Browser để lấy `.schema` thực tế trên máy bạn.

---

## 17. File: `declarative_performance_observer.db` (SQLite, nội bộ)
File dùng nội bộ để log hiệu năng của "declarative net request" / extension API performance observer. Chưa tìm thấy tài liệu công khai mô tả chi tiết tên bảng/cột — khuyến nghị mở trực tiếp bằng DB Browser for SQLite để lấy `.schema` chính xác cho bản Chrome đang dùng.

---

## 18. File: `engine_allowlist.bf` (không phải SQLite)
Đuôi `.bf` = **Bloom Filter** nhị phân (binary blob), không phải SQLite database. Dùng để tra cứu nhanh danh sách engine/tên miền được phép cho một số tính năng bảo mật nội bộ (ví dụ liên quan Safe Browsing / extension allowlist). Không có cấu trúc bảng/cột.

---

## 19. File: `trusted_vault.pb` (không phải SQLite)
File **Protocol Buffer** nhị phân, lưu dữ liệu "Trusted Vault" phục vụ mã hoá đồng bộ (Sync) nâng cao (liên quan security domain key cho Passkeys/Sync). Không phải SQLite, không có bảng/cột SQL.

---

## 20. File: `BookmarkMergedSurfaceOrdering` (không phải SQLite)
File JSON/nội bộ lưu thứ tự hiển thị hợp nhất giữa bookmark cục bộ và bookmark tài khoản (tính năng gộp bookmark theo tài khoản của Chrome mới). Chưa có tài liệu công khai mô tả chi tiết field.

---

## Bảng tổng hợp định dạng

| File | Định dạng |
|---|---|
| Account Web Data, Affiliation Database, BrowsingTopicsSiteData, Cookies, DIPS, Extension Cookies, Favicons, History, Login Data, Login Data For Account, MediaDeviceSalts, Shortcuts, Top Sites, Web Data, declarative_performance_observer.db | **SQLite** |
| Preferences, PreferredApps, BookmarkMergedSurfaceOrdering | **JSON** |
| Translate Ranker Model, trusted_vault.pb, BrowsingTopicsState | **Protobuf nhị phân** |
| engine_allowlist.bf | **Bloom filter nhị phân** |

## Cách tự kiểm tra schema chính xác trên máy bạn
Vì Chromium thay đổi schema qua từng bản, cách chắc chắn nhất là mở trực tiếp:
```
sqlite3 "Login Data"
.tables
.schema <tên_bảng>
```
(đóng Chrome trước, hoặc copy file ra chỗ khác, vì Chrome khoá file khi đang chạy).

## Nguồn tham khảo
- renenyffenegger.ch – Chrome user-data-directory notes
- dfir.blog – Chrome Values Lookup Tables
- atropos4n6.com – Google Chrome "Login Data" Forensics
- CyberArk – Online Credit Card Theft research
- Wikiversity – Chromium browsing history database
- Grokipedia / gist.github.com (creachadair) – Chrome cookie encryption format
- Mã nguồn Chromium (chromium.googlesource.com) cho các file ít tài liệu (Affiliation Database, DIPS, MediaDeviceSalts)
