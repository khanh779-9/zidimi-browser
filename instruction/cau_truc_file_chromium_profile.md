# Cấu trúc các tệp trong thư mục Profile của trình duyệt Chromium

> Phạm vi: Chrome/Chromium và các trình duyệt dựa trên Chromium như Edge, Brave, Vivaldi, Cốc Cốc…  
> Ngày đối chiếu mã nguồn: 06/08/2026.  
> Nguồn chính: mã nguồn Chromium chính thức. Schema thực tế có thể khác theo phiên bản, hệ điều hành, cờ tính năng và bản fork của trình duyệt.

## Kết luận nhanh

**Không phải tất cả các tệp trong `User Data\<Profile>` đều là SQLite.**

- Tệp SQLite thường bắt đầu bằng 16 byte: `SQLite format 3\0`.
- Tệp JSON thường bắt đầu bằng `{` hoặc `[`, có thể đọc bằng trình soạn thảo văn bản.
- Tệp `.pb` thường là Protocol Buffers dạng nhị phân.
- Tệp `.bf` thường là Bloom filter dạng nhị phân.
- Một số tệp mô hình máy học là binary/protobuf và không có khái niệm bảng/cột.

Hầu hết cơ sở dữ liệu SQLite của Chromium đều có bảng quản lý phiên bản:

- `meta(key, value)`

Trong mã nguồn Chromium, schema phổ biến của bảng này là:

```sql
meta(
    key LONGVARCHAR NOT NULL UNIQUE PRIMARY KEY,
    value LONGVARCHAR
)
```

---

## 1. File: `Account Web Data`

**Định dạng:** SQLite.  
**Mục đích:** dữ liệu Autofill gắn với tài khoản Google/Chromium account store. Đây là một `WebDatabase` riêng; tập bảng thực tế thường là một **tập con** của `Web Data` và thay đổi nhiều theo phiên bản.

### Schema hiện hành thường gặp

a. `meta(key, value)`

b. `addresses(guid, record_type, use_count, use_date, date_modified, language_code, label, initial_creator_id)`

c. `address_type_tokens(guid, type, value, verification_status, observations)`

d. `autofill_sync_metadata(model_type, storage_key, value)`

e. `autofill_model_type_state(model_type, value)`

f. `loyalty_cards(loyalty_card_id, merchant_name, program_name, program_logo, loyalty_card_number)` — có thể xuất hiện khi tính năng Valuables/Loyalty Cards được bật.

g. `loyalty_card_merchant_domain(loyalty_card_id, merchant_domain)`

h. `valuables_metadata(valuable_id, use_count, use_date)`

### Tên bảng cũ có thể còn sau nâng cấp

i. `contact_info(guid, use_count, use_date, date_modified, language_code, label, initial_creator_id, last_modifier_id)`

j. `contact_info_type_tokens(guid, type, value, verification_status)`

> Chromium mới đã hợp nhất địa chỉ local và account vào `addresses` + `address_type_tokens`. Các database đã nâng cấp qua nhiều đời có thể vẫn chứa bảng cũ hoặc bảng rỗng do migration.

---

## 2. File: `Affiliation Database`

**Định dạng:** SQLite.  
**Mục đích:** lưu các nhóm website/app được coi là có quan hệ đăng nhập tương đương, hỗ trợ Password Manager và Digital Asset Links.

a. `meta(key, value)`

b. `eq_classes(id, last_update_time)`

c. `eq_class_members(id, facet_uri, set_id, facet_display_name, facet_icon_url)`

Quan hệ chính:

- `eq_class_members.set_id` tham chiếu `eq_classes.id`.
- `facet_uri` thường là duy nhất.

---

## 3. File: `BookmarkMergedSurfaceOrdering`

**Định dạng:** JSON, **không phải SQLite**.  
**Không có bảng/cột SQL.**

Cấu trúc cấp cao:

a. `bookmark_bar`: mảng ID node bookmark theo thứ tự hiển thị.

b. `other`: mảng ID node trong “Other bookmarks”.

c. `mobile`: mảng ID node bookmark mobile.

Ví dụ dạng cấu trúc:

```json
{
  "bookmark_bar": ["12", "35", "81"],
  "other": ["27", "44"],
  "mobile": ["63"]
}
```

Các phần tử được lưu dưới dạng chuỗi biểu diễn ID của `BookmarkNode`.

---

## 4. File: `BrowsingTopicsSiteData`

**Định dạng:** SQLite ở các phiên bản Chromium còn sử dụng Topics API storage.  
**Trạng thái:** tính năng và schema này đã thay đổi/được gỡ khỏi một số nhánh Chromium mới; file có thể là dữ liệu legacy.

a. `meta(key, value)`

b. `browsing_topics_api_usage(hashed_main_frame_host, hashed_context_domain, last_usage_time)`

Khóa thường dùng:

- Khóa ghép: `(hashed_main_frame_host, hashed_context_domain)`.

c. `hashed_to_unhashed_domains(hashed_domain, unhashed_domain)`

> Tên bảng/cột trên thuộc schema Topics Site Data được Chromium sử dụng trong các nhánh triển khai Topics API. Một số phiên bản có thể bỏ bảng ánh xạ domain hoặc thay đổi chính sách lưu trữ.

---

## 5. File: `BrowsingTopicsState`

**Định dạng:** JSON, **không phải SQLite**.  
**Không có bảng/cột SQL.**

Các key cấp cao thường gặp:

a. `epochs`

b. `next_scheduled_calculation_time`

c. `hex_encoded_hmac_key`

d. `config_version`

Bên trong mỗi phần tử của `epochs` còn có dữ liệu topic, domain quan sát, thời gian tính toán và phiên bản taxonomy/model; cấu trúc con có thể thay đổi theo phiên bản.

---

## 6. File: `declarative_performance_observer.db`

**Định dạng:** SQLite.  
**Mục đích:** lưu policy và report của Declarative Performance Observer.

a. `meta(key, value)`

b. `declarative_performance_observer_policies(origin, capture_early_failures)`

- `origin`: khóa chính.
- `capture_early_failures`: boolean.

c. `declarative_performance_observer_reports(id, origin, payload, created_at)`

- `id`: `INTEGER PRIMARY KEY AUTOINCREMENT`.
- `payload`: nội dung report dạng text/JSON serialization.
- Có index theo `origin` trong các phiên bản hiện hành.

---

## 7. File: `DIPS`

**Định dạng:** SQLite.  
**Mục đích:** DIPS/BTM — theo dõi site storage, bounce tracking và tương tác người dùng để chống theo dõi chuyển hướng.

Tên thành phần mới trong Chromium là **BTM — Bounce Tracking Mitigations**, nhưng profile cũ hoặc một số bản vẫn dùng tên file `DIPS`.

a. `meta(key, value)`

b. `bounces(site, first_site_storage_time, last_site_storage_time, first_user_interaction_time, last_user_interaction_time, first_stateful_bounce_time, last_stateful_bounce_time, first_bounce_time, last_bounce_time, first_web_authn_assertion_time, last_web_authn_assertion_time)`

c. `popups(opener_site, popup_site, access_id, last_popup_time, is_current_interaction, is_authentication_interaction)` — xuất hiện ở các schema BTM/DIPS mới hơn.

d. `config(key, int_value)` — có thể xuất hiện trong các phiên bản mới để lưu cấu hình/giá trị nội bộ.

> Schema `DIPS` thay đổi khá nhanh. Database cũ có thể chỉ có `bounces`; database mới có thể thêm `popups`, `config` hoặc cột mới.

---

## 8. File: `engine_allowlist.bf`

**Định dạng:** Bloom filter nhị phân (`.bf`), **không phải SQLite**.

- Không có bảng.
- Không có cột.
- Dữ liệu là bit-array và metadata/hash parameters được parser tương ứng của Chromium đọc.
- Không thể liệt kê trực tiếp từng domain/engine chỉ bằng cách mở như database SQL.

---

## 9. File: `Extension Cookies`

**Định dạng:** SQLite.  
**Mục đích:** cookie store dành cho extension hoặc partition/profile store tương ứng. Schema dùng chung họ `SQLitePersistentCookieStore` của Chromium.

a. `meta(key, value)`

b. `cookies(creation_utc, host_key, top_frame_site_key, name, value, encrypted_value, path, expires_utc, is_secure, is_httponly, last_access_utc, has_expires, is_persistent, priority, samesite, source_scheme, source_port, last_update_utc, source_type, has_cross_site_ancestor)`

Các cột đáng chú ý:

- `encrypted_value`: giá trị cookie đã mã hóa; trên nhiều nền tảng `value` để trống.
- `top_frame_site_key`: partition key của cookie partitioned.
- `source_scheme`, `source_port`: nguồn tạo cookie.
- `has_cross_site_ancestor`: thành phần mới của khóa/partition semantics.

> Chromium hiện hành đã bỏ cột cũ như `is_same_party`; database nâng cấp từ bản cũ có thể từng có cột này trong lịch sử migration.

---

## 10. File: `Favicons`

**Định dạng:** SQLite.

a. `meta(key, value)`

b. `icon_mapping(id, page_url, icon_id, page_url_type)`

c. `favicons(id, url, icon_type)`

d. `favicon_bitmaps(id, icon_id, last_updated, image_data, width, height, last_requested)`

Quan hệ:

- `icon_mapping.icon_id` trỏ đến `favicons.id`.
- `favicon_bitmaps.icon_id` trỏ đến icon logic trong `favicons`.
- Một favicon có thể có nhiều bitmap theo kích thước.

---

## 11. File: `Login Data`

**Định dạng:** SQLite.  
**Mục đích:** password store local/profile.

a. `meta(key, value)`

b. `logins(origin_url, action_url, username_element, username_value, password_element, password_value, submit_element, signon_realm, date_created, blocklisted_by_user, scheme, password_type, times_used, form_data, display_name, icon_url, federation_url, skip_zero_click, generation_upload_status, possible_username_pairs, id, date_last_used, moving_blocked_for, date_password_modified, sender_email, sender_name, date_received, sharing_notification_displayed, sender_profile_image_url, date_last_filled, actor_login_approved)`

Ghi chú:

- `password_value` là BLOB được OS crypt/keychain bảo vệ, không phải plaintext.
- Một số cột chỉ xuất hiện trên một nền tảng hoặc sau một migration nhất định.
- Các bản cũ có thể có `preferred`, `date_synced`, `possible_usernames` hoặc tên cột cũ khác.

c. `stats(origin_domain, username_value, dismissal_count, update_time)`

d. `insecure_credentials(parent_id, insecurity_type, create_time, is_muted, trigger_notification_from_backend)`

e. `password_notes(id, parent_id, key, value, date_created, confidential)`

f. `passwords_sync_entities_metadata(storage_key, metadata)`

g. `passwords_sync_model_metadata(id, model_metadata)`

Quan hệ:

- `insecure_credentials.parent_id` tham chiếu `logins.id`.
- `password_notes.parent_id` tham chiếu `logins.id`.

---

## 12. File: `Login Data For Account`

**Định dạng:** SQLite.  
**Mục đích:** password store gắn với tài khoản, tách khỏi local profile store.

Schema nhìn chung dùng cùng implementation với `Login Data`:

a. `meta(key, value)`

b. `logins(origin_url, action_url, username_element, username_value, password_element, password_value, submit_element, signon_realm, date_created, blocklisted_by_user, scheme, password_type, times_used, form_data, display_name, icon_url, federation_url, skip_zero_click, generation_upload_status, possible_username_pairs, id, date_last_used, moving_blocked_for, date_password_modified, sender_email, sender_name, date_received, sharing_notification_displayed, sender_profile_image_url, date_last_filled, actor_login_approved)`

c. `stats(origin_domain, username_value, dismissal_count, update_time)`

d. `insecure_credentials(parent_id, insecurity_type, create_time, is_muted, trigger_notification_from_backend)`

e. `password_notes(id, parent_id, key, value, date_created, confidential)`

f. `passwords_sync_entities_metadata(storage_key, metadata)`

g. `passwords_sync_model_metadata(id, model_metadata)`

Khác biệt chính là **phạm vi dữ liệu/account store**, không phải một schema hoàn toàn khác.

---

## 13. File: `MediaDeviceSalts`

**Định dạng:** SQLite.  
**Mục đích:** lưu salt theo StorageKey để tạo định danh thiết bị media ổn định nhưng tách biệt theo site.

a. `meta(key, value)`

b. `media_device_salts(storage_key, creation_time, salt)`

- `storage_key`: khóa chính.
- Thường có index trên `creation_time` để dọn dữ liệu theo thời gian.

---

## 14. File: `Top Sites`

**Định dạng:** SQLite.  
**Mục đích:** lưu danh sách website thường truy cập cho New Tab Page/Most Visited.

a. `meta(key, value)`

b. `top_sites(url, url_rank, title, redirects)`

- `url`: khóa chính.
- `redirects`: cột legacy vẫn có thể tồn tại dù ít/không còn được dùng.

---

## 15. File: `Preferences`

**Định dạng:** JSON, **không phải SQLite**.

Không có bảng/cột cố định. Đây là một object JSON lớn với key động theo:

- phiên bản trình duyệt;
- hệ điều hành;
- extension đã cài;
- feature flags;
- chính sách doanh nghiệp;
- trạng thái profile.

Các nhóm key thường gặp, chỉ mang tính minh họa:

a. `account_info`

b. `autofill`

c. `bookmark_bar`

d. `browser`

e. `credentials_enable_service`

f. `download`

g. `extensions`

h. `intl`

i. `media`

j. `profile`

k. `safebrowsing`

l. `session`

m. `signin`

n. `translate`

> Không nên xây parser dựa trên một danh sách key đóng. Hãy đọc JSON linh hoạt và kiểm tra key tồn tại.

---

## 16. File: `PreferredApps`

**Định dạng:** thường là JSON trên các nền tảng/build có tính năng Preferred Apps; **không phải SQLite**.

Không có bảng/cột SQL. Cấu trúc có thể gồm:

a. danh sách preferred app entries;

b. app ID;

c. intent filter / URL scope;

d. loại xử lý liên kết;

e. metadata phiên bản.

> File này phụ thuộc mạnh vào ChromeOS/Android hoặc component quản lý app của từng browser. Cần kiểm tra byte đầu để xác nhận JSON ở profile cụ thể.

---

## 17. File: `Shortcuts`

**Định dạng:** SQLite.  
**Mục đích:** lưu shortcut học được của Omnibox.

a. `meta(key, value)`

b. `omni_box_shortcuts(id, text, fill_into_edit, url, document_type, contents, contents_class, description, description_class, transition, type, keyword, last_access_time, number_of_hits)`

---

## 18. File: `Translate Ranker Model`

**Định dạng:** mô hình nhị phân/serialized model, **không phải SQLite**.

- Không có bảng.
- Không có cột.
- Nội dung thường là model/ranker data được component Translate tải và deserialize.
- Schema nhị phân phụ thuộc phiên bản model và implementation của trình duyệt.

---

## 19. File: `trusted_vault.pb`

**Định dạng:** Protocol Buffers nhị phân (`.pb`), **không phải SQLite**.

- Không có bảng/cột SQL.
- Chứa trạng thái/dữ liệu Trusted Vault phục vụ sync encryption, key rotation hoặc recovery-related metadata.
- Phải parse bằng đúng `.proto` và phiên bản message của Chromium; không nên suy diễn bằng cách đọc text.

> Đây là dữ liệu nhạy cảm liên quan đến khóa/trạng thái mã hóa. Chỉ nên đọc trên bản sao phục vụ debug; không sửa trực tiếp.

---

## 20. File: `Web Data`

**Định dạng:** SQLite.  
**Mục đích:** cơ sở dữ liệu tổng hợp cho Autofill, payment data, search engines, token service và metadata đồng bộ. Đây là một trong các file có schema thay đổi nhiều nhất.

### 20.1. Bảng chung

a. `meta(key, value)`

### 20.2. Autofill autocomplete

b. `autofill(name, value, value_lower, date_created, date_last_used, count)`

### 20.3. Address schema mới

c. `addresses(guid, record_type, use_count, use_date, date_modified, language_code, label, initial_creator_id)`

d. `address_type_tokens(guid, type, value, verification_status, observations)`

### 20.4. Address schema cũ/legacy có thể còn

e. `autofill_profiles(guid, company_name, street_address, dependent_locality, city, state, zipcode, sorting_code, country_code, use_count, use_date, date_modified, language_code, label, disallow_settings_visible_updates)`

f. `autofill_profile_names(guid, first_name, middle_name, last_name, first_last_name, conjunction_last_name, second_last_name, full_name, first_name_status, middle_name_status, last_name_status, first_last_name_status, conjunction_last_name_status, second_last_name_status, full_name_status)`

g. `autofill_profile_emails(guid, email)`

h. `autofill_profile_phones(guid, number)`

i. `autofill_profile_birthdates(guid, day, month, year)`

j. `autofill_profile_addresses(guid, street_address, street_name, dependent_street_name, house_number, subpremise, dependent_locality, city, state, zip_code, country_code, sorting_code, apartment_number, floor, street_address_status, street_name_status, dependent_street_name_status, house_number_status, subpremise_status, dependent_locality_status, city_status, state_status, zip_code_status, country_code_status, sorting_code_status, apartment_number_status, floor_status)`

k. `local_addresses(guid, use_count, use_date, date_modified, language_code, label, initial_creator_id, last_modifier_id)` — schema trung gian cũ.

l. `local_addresses_type_tokens(guid, type, value, verification_status)`

m. `contact_info(guid, use_count, use_date, date_modified, language_code, label, initial_creator_id, last_modifier_id)`

n. `contact_info_type_tokens(guid, type, value, verification_status)`

### 20.5. Credit card và payment

o. `credit_cards(guid, name_on_card, expiration_month, expiration_year, card_number_encrypted, use_count, use_date, date_modified, is_user_confirmed, billing_address_id, nickname)`

p. `masked_credit_cards(id, status, name_on_card, network, last_four, exp_month, exp_year, bank_name, nickname, card_issuer, card_issuer_id, instrument_id, virtual_card_enrollment_state, virtual_card_enrollment_type, card_art_url, product_description, product_terms_url, card_info_retrieval_enrollment_state, card_benefit_source, card_creation_source)`

q. `server_card_cloud_token_data(id, suffix, exp_month, exp_year, card_art_url, instrument_token)`

r. `server_card_metadata(id, use_count, use_date, billing_address_id)`

s. `local_ibans(guid, use_count, use_date, value_encrypted, nickname)`

t. `masked_ibans(instrument_id, prefix, suffix, nickname)`

u. `masked_ibans_metadata(instrument_id, use_count, use_date)`

v. `payments_customer_data(customer_id)`

w. `payments_upi_vpa(vpa)`

x. `offer_data(offer_id, offer_reward_amount, expiry, offer_details_url, promo_code, value_prop_text, see_details_text, usage_instructions_text)`

y. `offer_eligible_instrument(offer_id, instrument_id)`

z. `offer_merchant_domain(offer_id, merchant_domain)`

aa. `virtual_card_usage_data(id, instrument_id, merchant_domain, last_four)`

ab. `local_stored_cvc(guid, value_encrypted, last_updated_timestamp)`

ac. `server_stored_cvc(instrument_id, value_encrypted, last_updated_timestamp)`

ad. `masked_bank_accounts(instrument_id, bank_name, account_number_suffix, account_type, display_icon_url, nickname)`

ae. `masked_bank_accounts_metadata(instrument_id, use_count, use_date)`

af. `masked_credit_card_benefits(benefit_id, instrument_id, benefit_type, benefit_category, benefit_description, start_time, end_time)`

ag. `benefit_merchant_domains(benefit_id, merchant_domain)`

> Các bản mới còn có thể thêm bảng payment instruments, e-wallet, BNPL hoặc creation options. Hãy dùng script ở cuối tài liệu để lấy đúng schema của file thực tế.

### 20.6. Valuables/Loyalty Cards

ah. `loyalty_cards(loyalty_card_id, merchant_name, program_name, program_logo, loyalty_card_number)`

ai. `loyalty_card_merchant_domain(loyalty_card_id, merchant_domain)`

aj. `valuables_metadata(valuable_id, use_count, use_date)`

### 20.7. Sync metadata

ak. `autofill_sync_metadata(model_type, storage_key, value)`

al. `autofill_model_type_state(model_type, value)`

### 20.8. Search engine/Omnibox provider

am. `keywords(id, short_name, keyword, favicon_url, url, safe_for_autoreplace, originating_url, date_created, usage_count, input_encodings, suggest_url, prepopulate_id, created_by_policy, last_modified, sync_guid, alternate_urls, image_url, search_url_post_params, suggest_url_post_params, image_url_post_params, new_tab_url, last_visited, starter_pack_id, enforced_by_policy, featured_by_policy)`

> Danh sách cột `keywords` thay đổi theo version. Các bản cũ có thể có `show_in_default_list`, `autogenerate_keyword`, `instant_url`, `search_terms_replacement_key`, `logo_url`, `doodle_url`, v.v.

### 20.9. Token service

an. `token_service(service, encrypted_token)`

> Tùy browser/build, `token_service` có thể nằm trong Web Data hoặc được chuyển sang store/component khác.

---

## 21. File: `History`

**Định dạng:** SQLite.  
**Mục đích:** lịch sử URL, visit graph, search terms, downloads, segments, annotations và history clusters.

### 21.1. Bảng chung và URL

a. `meta(key, value)`

b. `urls(id, url, title, visit_count, typed_count, last_visit_time, hidden)`

c. `keyword_search_terms(keyword_id, url_id, term, normalized_term)`

### 21.2. Visits

d. `visits(id, url, visit_time, from_visit, external_referrer_url, transition, segment_id, visit_duration, incremented_omnibox_typed_score, opener_visit, originator_cache_guid, originator_visit_id, originator_from_visit, originator_opener_visit, is_known_to_sync, consider_for_ntp_most_visited, visited_link_id, app_id)`

e. `visit_source(id, source)`

### 21.3. Segments

f. `segments(id, name, url_id)`

g. `segment_usage(id, segment_id, time_slot, visit_count)`

### 21.4. Downloads

h. `downloads(id, guid, current_path, target_path, start_time, received_bytes, total_bytes, state, danger_type, interrupt_reason, hash, end_time, opened, last_access_time, transient, referrer, site_url, embedder_download_data, tab_url, tab_referrer_url, http_method, by_ext_id, by_ext_name, by_web_app_id, etag, last_modified, mime_type, original_mime_type)`

i. `downloads_url_chains(id, chain_index, url)`

j. `downloads_slices(download_id, offset, received_bytes, finished)`

### 21.5. Visit annotations

k. `content_annotations(visit_id, visibility_score, floc_protected_score, categories, page_topics_model_version, annotation_flags, entities, related_searches, search_normalized_url, search_terms, alternative_title, page_language, password_state, has_url_keyed_image)`

> `floc_protected_score` là cột legacy; code mới có thể không đọc/ghi nó nhưng bảng nâng cấp vẫn còn cột.

l. `context_annotations(visit_id, context_annotation_flags, duration_since_last_visit, page_end_reason, total_foreground_duration, browser_type, window_id, tab_id, task_id, root_task_id, parent_task_id, response_code)`

### 21.6. History clusters

m. `clusters(cluster_id, should_show_on_prominent_ui_surfaces, label, raw_label, triggerability_calculated, originator_cache_guid, originator_cluster_id)`

n. `clusters_and_visits(cluster_id, visit_id, score, engagement_score, url_for_deduping, normalized_url, url_for_display, interaction_state)`

o. `cluster_keywords(cluster_id, keyword, type, score, collections)`

p. `cluster_visit_duplicates(visit_id, duplicate_visit_id)`

### 21.7. Sync metadata có thể xuất hiện

q. `history_sync_metadata(storage_key, value)`

r. `history_sync_model_metadata(id, value)`

> Tên và sự tồn tại của hai bảng sync metadata phụ thuộc thế hệ schema/implementation sync.

---

# Cách lấy schema **chính xác 100%** từ profile trên máy

Danh sách phía trên được đối chiếu từ Chromium nhưng không thể thay thế việc đọc file thực tế, vì Chrome/Edge/Brave/Cốc Cốc có thể dùng schema khác nhau.

## Cách 1: dùng SQLite CLI

1. **Đóng hoàn toàn trình duyệt**.
2. Copy file cần đọc sang thư mục khác; nên copy cả `-wal` và `-shm` nếu có.
3. Chạy:

```bash
sqlite3 "History"
.tables
.schema
```

Xem từng bảng:

```sql
PRAGMA table_info('visits');
PRAGMA foreign_key_list('visits');
PRAGMA index_list('visits');
```

## Cách 2: script Python xuất toàn bộ bảng và cột thành Markdown

```python
from __future__ import annotations

import shutil
import sqlite3
from pathlib import Path


def is_sqlite(path: Path) -> bool:
    try:
        with path.open("rb") as f:
            return f.read(16) == b"SQLite format 3\x00"
    except OSError:
        return False


def quote_identifier(name: str) -> str:
    return '"' + name.replace('"', '""') + '"'


def dump_schema(database_file: Path) -> list[str]:
    lines: list[str] = []

    if not is_sqlite(database_file):
        return [f"## File: `{database_file.name}`", "", "Không phải SQLite.", ""]

    # Đọc trên bản copy để tránh lock/WAL thay đổi khi trình duyệt đang chạy.
    copy_path = database_file.with_name(database_file.name + ".schema-copy")
    shutil.copy2(database_file, copy_path)

    try:
        uri = f"file:{copy_path.as_posix()}?mode=ro"
        with sqlite3.connect(uri, uri=True) as conn:
            tables = conn.execute(
                """
                SELECT name
                FROM sqlite_schema
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
                ORDER BY name
                """
            ).fetchall()

            lines.extend([f"## File: `{database_file.name}`", ""])
            for index, (table_name,) in enumerate(tables, start=1):
                pragma = f"PRAGMA table_info({quote_identifier(table_name)})"
                columns = conn.execute(pragma).fetchall()
                column_text = ", ".join(row[1] for row in columns)
                lines.append(f"{index}. `{table_name}({column_text})`")

            lines.append("")
            return lines
    finally:
        copy_path.unlink(missing_ok=True)


def main() -> None:
    profile_dir = Path(r"C:\Users\<USER>\AppData\Local\Google\Chrome\User Data\Default")
    output = Path("profile_sqlite_schemas.md")

    lines = ["# SQLite schemas trong Chromium profile", ""]
    for path in sorted(profile_dir.iterdir(), key=lambda p: p.name.lower()):
        if path.is_file() and is_sqlite(path):
            lines.extend(dump_schema(path))

    output.write_text("\n".join(lines), encoding="utf-8")
    print(f"Đã ghi: {output.resolve()}")


if __name__ == "__main__":
    main()
```

## Cách 3: nhận diện loại file bằng Python

```python
from pathlib import Path


def detect_file_type(path: Path) -> str:
    data = path.read_bytes()[:64]

    if data.startswith(b"SQLite format 3\x00"):
        return "SQLite"

    stripped = data.lstrip()
    if stripped.startswith((b"{", b"[")):
        return "JSON hoặc text JSON"

    if path.suffix.lower() == ".pb":
        return "Có khả năng là Protocol Buffers"

    if path.suffix.lower() == ".bf":
        return "Có khả năng là Bloom filter"

    return "Binary/định dạng khác"
```

---

# Lưu ý an toàn khi đọc profile

- Đóng trình duyệt hoặc chỉ đọc trên **bản sao**, vì SQLite WAL có thể đang được ghi.
- Nếu copy khi trình duyệt đang chạy, cần copy đồng thời file chính, `-wal` và `-shm`; nếu không dữ liệu có thể thiếu hoặc không nhất quán.
- Không sửa trực tiếp `Login Data`, `Cookies`, `Web Data`, `trusted_vault.pb` hay `Preferences`; sai một byte có thể khiến browser reset database hoặc mất dữ liệu.
- Password/cookie/token thường được mã hóa bởi OS crypt/DPAPI/Keychain; nhìn thấy BLOB không có nghĩa là có plaintext.
- Trình duyệt fork có thể thêm bảng riêng ngoài schema Chromium.

---

# Nguồn mã Chromium chính thức đã đối chiếu

- SQLite Cookie Store: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/net/extras/sqlite/sqlite_persistent_cookie_store.cc>
- Login Database: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/password_manager/core/browser/password_store/login_database.cc>
- Affiliation Database: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/affiliations/core/browser/affiliation_database.cc>
- Favicons: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/favicon/core/favicon_database.cc>
- Top Sites: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/top_sites_database.cc>
- Shortcuts: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/omnibox/browser/shortcuts_database.cc>
- History URL database: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/url_database.cc>
- History visits: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/visit_database.cc>
- History downloads: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/download_database.cc>
- History annotations/clusters: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/visit_annotations_database.cc>
- Address Autofill tables: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/autofill/core/browser/webdata/addresses/address_autofill_table.cc>
- Payment Autofill tables: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/autofill/core/browser/webdata/payments/payments_autofill_table.cc>
- Autofill sync metadata: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/autofill/core/browser/webdata/autofill_sync_metadata_table.cc>
- Valuables/Loyalty Cards: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/autofill/core/browser/webdata/valuables/valuables_table.cc>
- Bookmark merged ordering: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/chrome/browser/bookmarks/bookmark_merged_surface_ordering_storage.h>
- Browsing Topics state: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/browsing_topics/browsing_topics_state.cc>
- Browsing Topics site storage: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/content/browser/browsing_topics/browsing_topics_site_data_storage.cc>
- DIPS/BTM database: <https://chromium.googlesource.com/chromium/src/+/refs/heads/main/content/browser/btm/>

