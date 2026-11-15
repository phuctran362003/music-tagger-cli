# SOFTWARE REQUIREMENTS SPECIFICATION (SRS)

## MusicTagger CLI -- Công cụ chuẩn hóa metadata thư viện nhạc MP3

-   **Phiên bản tài liệu:** 1.0
-   **Ngày:** 15/11/2026
-   **Trạng thái:** Baseline cho MVP
-   **Ngôn ngữ:** Tiếng Việt
-   **Nền tảng mục tiêu:** .NET 10, cross-platform (Windows, Linux,
    macOS)

------------------------------------------------------------------------

## 1. Giới thiệu

### 1.1. Mục đích

Tài liệu này mô tả yêu cầu chức năng, phi chức năng, quy tắc nghiệp vụ,
kiến trúc định hướng và tiêu chí nghiệm thu cho **MusicTagger CLI**. Tài
liệu là nguồn yêu cầu chính thức để AI coding agent hoặc developer thiết
kế, triển khai và kiểm thử sản phẩm.

### 1.2. Bối cảnh và vấn đề

Nhiều file MP3 được tải từ Internet chỉ phát được âm thanh nhưng thiếu
hoặc sai metadata. Các MP3 Player thường tổ chức thư viện theo Artist,
Album, Title hoặc Track Number; do đó file thiếu metadata có thể hiển
thị trong mục Unknown, sai thứ tự hoặc khó tìm kiếm.

MusicTagger CLI cho phép người dùng quét một thư mục, chọn một hoặc
nhiều file MP3, chỉnh metadata theo từng file hoặc hàng loạt, xem trước
thay đổi và áp dụng an toàn.

### 1.3. Mục tiêu sản phẩm

1.  Đọc và chỉnh sửa metadata MP3 phổ biến.
2.  Chỉnh một file, nhiều file được chọn hoặc toàn bộ MP3 trong thư mục.
3.  Hỗ trợ tiếng Việt Unicode end-to-end, không lỗi dấu sau khi ghi và
    đọc lại.
4.  Có preview trước khi ghi, backup metadata cũ và khả năng restore.
5.  Có giao diện terminal dễ hiểu, màu sắc hợp lý, bảng dữ liệu, prompt,
    progress và loading.
6.  Không làm thay đổi dữ liệu âm thanh của file MP3.

### 1.4. Ngoài phạm vi MVP

-   Tải nhạc hoặc tìm metadata tự động từ Internet.
-   Nhận diện bài hát bằng audio fingerprint.
-   Chỉnh sửa nội dung âm thanh, cắt ghép, chuyển mã hoặc tăng giảm
    bitrate.
-   Quản lý mọi định dạng audio ngoài MP3.
-   GUI/desktop application.
-   Tự động tải hoặc tìm album art trực tuyến.

Các mục trên có thể được xem xét ở phiên bản sau.

### 1.5. Thuật ngữ

  Thuật ngữ           Ý nghĩa
  ------------------- --------------------------------------------------
  Metadata / Tag      Thông tin mô tả được nhúng trong file audio
  ID3                 Chuẩn metadata phổ biến của MP3
  ID3v2.3 / ID3v2.4   Các phiên bản ID3v2
  Batch edit          Chỉnh metadata cho nhiều file trong một thao tác
  Preview / Dry run   Hiển thị thay đổi dự kiến mà chưa ghi file
  Backup manifest     File JSON lưu metadata trước khi chỉnh
  Restore             Khôi phục metadata từ backup
  Root folder         Thư mục người dùng chọn để quét

------------------------------------------------------------------------

## 2. Người dùng và use case

### 2.1. Nhóm người dùng

Người dùng phổ thông có bộ sưu tập MP3 cá nhân và sử dụng MP3 Player,
thiết bị nghe nhạc offline, điện thoại hoặc phần mềm quản lý nhạc.

Không yêu cầu người dùng có kiến thức về ID3 hoặc lập trình.

### 2.2. Use case chính

**UC-01 -- Chọn thư mục nhạc** 1. App yêu cầu nhập đường dẫn thư mục. 2.
Người dùng paste hoặc nhập đường dẫn. 3. App kiểm tra thư mục tồn tại và
có quyền đọc. 4. App quét các file `.mp3` trong thư mục theo chế độ đã
chọn. 5. App hiển thị số lượng file tìm thấy và danh sách file.

**UC-02 -- Chọn phạm vi chỉnh sửa** - Người dùng chọn một file. - Người
dùng chọn nhiều file bằng danh sách số thứ tự. - Người dùng chọn toàn bộ
file MP3 trong kết quả quét.

**UC-03 -- Chỉnh một file** 1. App đọc metadata hiện tại. 2. App hiển
thị các trường metadata. 3. Người dùng nhập giá trị mới hoặc giữ nguyên
giá trị. 4. App hiển thị preview old/new. 5. Người dùng xác nhận hoặc
hủy. 6. App ghi metadata và xác minh bằng cách đọc lại.

**UC-04 -- Chỉnh hàng loạt** 1. Người dùng chọn nhiều file hoặc tất cả
file. 2. App cho phép đặt một hoặc nhiều trường có giá trị chung cho
toàn bộ selection. 3. App cho phép bỏ qua trường không muốn thay đổi. 4.
App preview số file và thay đổi dự kiến. 5. Người dùng xác nhận. 6. App
xử lý lần lượt, hiển thị tiến độ và kết quả từng file.

**UC-05 -- Khôi phục metadata** 1. Người dùng chọn chức năng Restore. 2.
App liệt kê backup manifest khả dụng. 3. Người dùng chọn manifest. 4.
App preview các file và trường sẽ được khôi phục. 5. Người dùng xác
nhận. 6. App restore metadata và báo kết quả.

------------------------------------------------------------------------

## 3. Phạm vi metadata

### 3.1. Các trường MVP

  Trường         Mô tả               ID3 frame định hướng
  -------------- ------------------- -------------------------
  Title          Tên bài hát         TIT2
  Artist         Nghệ sĩ trình bày   TPE1
  Album          Tên album           TALB
  Album Artist   Nghệ sĩ của album   TPE2
  Genre          Thể loại            TCON
  Year           Năm phát hành       TDRC / TYER tùy version
  Track Number   Số thứ tự track     TRCK
  Disc Number    Số thứ tự đĩa       TPOS
  Composer       Nhạc sĩ sáng tác    TCOM
  Comment        Ghi chú             COMM

MVP phải hỗ trợ tối thiểu Title, Artist và Album. Các trường còn lại nên
được hỗ trợ nếu thư viện metadata được chọn xử lý ổn định.

### 3.2. Quy tắc chỉnh sửa

-   Giá trị rỗng trong form mặc định có nghĩa là **không thay đổi**,
    không phải xóa tag.
-   Cần có thao tác rõ ràng để xóa một trường metadata.
-   Với batch edit, mỗi trường có trạng thái `Keep`, `Set value` hoặc
    `Clear`.
-   Không được ghi đè các trường không nằm trong yêu cầu chỉnh sửa.
-   Không được xóa các frame/tag không liên quan nếu thư viện cho phép
    giữ lại.
-   App phải giữ nguyên audio stream; chỉ cập nhật tag.

### 3.3. ID3 version và encoding

-   Hỗ trợ đọc ID3v2.3 và ID3v2.4.
-   Mặc định ghi ID3v2.3 để ưu tiên tương thích với nhiều thiết bị MP3
    Player cũ.
-   Có cấu hình hoặc lựa chọn để ghi ID3v2.4.
-   Khi ghi ID3v2.3, sử dụng encoding Unicode tương thích của chuẩn
    (thường là UTF-16 cho text frame); không cố ghi UTF-8 vào text frame
    ID3v2.3.
-   Khi ghi ID3v2.4, có thể sử dụng UTF-8 nếu thư viện hỗ trợ đúng
    chuẩn.
-   Không được giả định mọi thiết bị cũ hỗ trợ tiếng Việt giống nhau.
    Cần mô tả giới hạn tương thích trong README.
-   Phải có test tiếng Việt có dấu, ký tự kết hợp Unicode và ký tự ngoài
    ASCII.

------------------------------------------------------------------------

## 4. Yêu cầu chức năng

### FR-001 -- Khởi chạy và menu chính

App phải hiển thị menu chính dễ hiểu, tối thiểu gồm: 1. Scan / Edit
music folder 2. Restore metadata backup 3. Settings 4. Exit

### FR-002 -- Nhập đường dẫn

-   Cho phép nhập đường dẫn bằng cách paste vào terminal.
-   Hỗ trợ đường dẫn có dấu cách và Unicode.
-   Chấp nhận đường dẫn tuyệt đối.
-   Loại bỏ cặp dấu nháy bao quanh nếu người dùng paste `"path"` hoặc
    `'path'`.
-   Không tự ý sửa hoặc chuẩn hóa đường dẫn theo cách làm thay đổi ý
    nghĩa.
-   Báo lỗi rõ ràng nếu thư mục không tồn tại hoặc không có quyền truy
    cập.

### FR-003 -- Quét MP3

-   Mặc định quét trực tiếp trong thư mục được chọn.
-   Có tùy chọn bật/tắt quét đệ quy thư mục con.
-   Chỉ xử lý extension `.mp3`, không phân biệt chữ hoa/chữ thường.
-   Không đi theo symbolic link/reparse point nếu có nguy cơ tạo vòng
    lặp hoặc thoát khỏi root folder; hành vi phải được xác định rõ và
    kiểm thử.
-   Hiển thị tổng số file tìm thấy.
-   Nếu không tìm thấy file, hướng dẫn người dùng chọn thư mục khác hoặc
    quay lại.

### FR-004 -- Đọc metadata

-   Đọc các trường được hỗ trợ.
-   Hiển thị giá trị thiếu bằng `—` hoặc `Unknown`, không tự ghi giá trị
    mặc định vào file.
-   Nếu một file lỗi hoặc không đọc được tag, ghi nhận lỗi riêng cho
    file đó và tiếp tục xử lý các file còn lại.

### FR-005 -- Chọn file

-   Chọn một file.
-   Chọn nhiều file bằng số thứ tự hoặc danh sách index.
-   Chọn tất cả.
-   Có lựa chọn quay lại mà không thay đổi dữ liệu.
-   Hiển thị tên file và metadata rút gọn trong bảng.

### FR-006 -- Chỉnh metadata đơn lẻ

-   Hiển thị giá trị hiện tại.
-   Cho phép nhập Title, Artist, Album và các trường bổ sung được hỗ
    trợ.
-   Người dùng có thể giữ nguyên, cập nhật hoặc xóa rõ ràng từng trường.
-   Validate kiểu dữ liệu, ví dụ Year và Track Number.
-   Không được coi chuỗi chỉ có khoảng trắng là giá trị hợp lệ đối với
    các trường bắt buộc về mặt nghiệp vụ; tuy nhiên không ép các trường
    metadata tùy chọn phải có giá trị.

### FR-007 -- Chỉnh metadata hàng loạt

-   Cho phép áp dụng cùng một giá trị cho một trường trên tất cả file
    được chọn.
-   Cho phép cập nhật nhiều trường trong một lần.
-   Có lựa chọn chỉ cập nhật một số trường.
-   Không thay đổi các trường được đánh dấu `Keep`.
-   Hỗ trợ batch trên toàn bộ file trong thư mục.
-   Phải hiển thị số lượng file mục tiêu trước khi xác nhận.

### FR-008 -- Preview / Dry run

Trước khi ghi file, app phải hiển thị: - Số file dự kiến được cập
nhật. - File nào sẽ được cập nhật. - Trường nào thay đổi. - Giá trị cũ
và giá trị mới. - File nào bị bỏ qua hoặc có cảnh báo. - Không ghi bất
kỳ thay đổi nào trong bước preview.

### FR-009 -- Xác nhận thao tác

-   Mặc định không tự động ghi file.
-   Yêu cầu xác nhận rõ ràng trước khi áp dụng.
-   Có lựa chọn Cancel.
-   Nếu selection có số lượng lớn, thông báo rõ số file sẽ bị tác động.
-   Không dùng phím Enter mặc định để vô tình xác nhận thao tác phá hủy
    dữ liệu nếu prompt có thể cấu hình an toàn hơn.

### FR-010 -- Backup trước khi ghi

-   Trước khi thay đổi file, lưu metadata cũ của từng file vào backup
    manifest JSON.
-   Backup mặc định nằm trong thư mục dữ liệu ứng dụng hoặc thư mục
    `.musictagger-backups` được cấu hình; không đặt lẫn vào thư mục nhạc
    nếu người dùng không yêu cầu.
-   Manifest gồm ID duy nhất, thời gian UTC, root folder, file path
    tương đối, metadata cũ, ID3 version nếu đọc được và trạng thái xử
    lý.
-   Không lưu nội dung audio vào JSON.
-   Backup phải được ghi thành công trước khi sửa file. Nếu backup không
    thành công, không thực hiện ghi file đó.
-   Có thể cung cấp tùy chọn backup folder riêng trong Settings.

### FR-011 -- Ghi metadata an toàn

-   Chỉ ghi các trường có thay đổi.
-   Dùng cơ chế ghi an toàn nếu thư viện hỗ trợ: ghi file tạm cùng
    filesystem, xác minh rồi thay thế file gốc.
-   Không được để file gốc bị mất nếu thao tác ghi thất bại.
-   Nếu không thể dùng atomic replace do giới hạn thư viện/OS, phải có
    fallback an toàn và nêu rõ trong tài liệu.
-   Không thay đổi nội dung audio stream.
-   Sau khi ghi, đọc lại metadata để xác minh các giá trị đã lưu.
-   Nếu xác minh thất bại, báo lỗi và hướng dẫn restore từ backup.

### FR-012 -- Progress và kết quả

-   Hiển thị progress bar hoặc spinner khi quét và xử lý batch.
-   Hiển thị số file đã xử lý / tổng số file.
-   Không chỉ dựa vào màu để thể hiện trạng thái; phải có text như
    Success, Warning, Failed.
-   Kết quả cuối cùng gồm số thành công, thất bại, bỏ qua.
-   Có danh sách lỗi theo file và nguyên nhân dễ hiểu.

### FR-013 -- Restore

-   Liệt kê các backup manifest.
-   Cho phép chọn manifest và preview.
-   Restore chỉ metadata được lưu trong manifest.
-   Trước khi restore, tạo backup mới của trạng thái hiện tại để có thể
    quay lại.
-   Nếu file không tồn tại, đánh dấu skipped và tiếp tục.
-   Không restore file nằm ngoài phạm vi root folder đã xác nhận, trừ
    khi người dùng chủ động xác nhận rõ ràng.

### FR-014 -- Settings

Các cấu hình tối thiểu: - ID3 write version: v2.3 (default) / v2.4. -
Recursive scan: Off (default) / On. - Backup directory. - Console color:
Auto / On / Off. - Ngôn ngữ UI: MVP chỉ cần tiếng Việt hoặc tiếng Anh
nhất quán; ưu tiên tiếng Anh trong code identifier, tiếng Việt trong tài
liệu và có thể dùng tiếng Việt trong UI. - Cấu hình lưu bằng JSON trong
thư mục dữ liệu ứng dụng.

### FR-015 -- Logging

-   Ghi log chẩn đoán ở mức phù hợp.
-   Không ghi nội dung âm thanh.
-   Không log thông tin nhạy cảm không cần thiết.
-   Log lỗi phải có file path và exception detail trong log file; UI chỉ
    hiển thị thông báo dễ hiểu.
-   Không để lỗi của một file làm dừng toàn bộ batch trừ khi lỗi nghiêm
    trọng ảnh hưởng tính toàn vẹn chung.

------------------------------------------------------------------------

## 5. Luồng giao diện CLI đề xuất

### 5.1. Menu chính

``` text
╭──────────────────────────────────────────────╮
│                 MUSIC TAGGER                 │
│       Organize your offline music library    │
╰──────────────────────────────────────────────╯

  1. Scan and edit MP3 folder
  2. Restore metadata backup
  3. Settings
  4. Exit

Select an option:
```

### 5.2. Luồng scan/edit

``` text
Enter music folder path:
> D:\Music\Nhạc Trịnh

Scanning folder...
Found 24 MP3 files.

Choose operation:
  1. Edit one file
  2. Select multiple files
  3. Edit all files
  4. Back
```

### 5.3. Batch edit

``` text
Selected: 24 files

Choose fields to update:
  [x] Artist
  [x] Album
  [ ] Title
  [ ] Genre
  [ ] Year

Artist value: Trịnh Công Sơn
Album value: Sơn Ca 7

Preview changes? [Y/n]
```

### 5.4. Preview

``` text
PREVIEW – No files have been changed

Files selected: 24
Files with changes: 24

Field       Old value       New value
Artist      Unknown         Trịnh Công Sơn
Album       Unknown         Sơn Ca 7

Apply these changes? [y/N]
```

### 5.5. Processing result

``` text
Processing MP3 files...
[████████████████████████████] 24/24

Completed
  Updated: 23
  Failed:   1
  Skipped:  0

Details:
  ! 08 - Track.mp3: file is read-only
```

UI phải hoạt động tốt trong terminal không hỗ trợ màu. Cần có fallback
không màu và không phụ thuộc vào emoji để truyền đạt thông tin.

------------------------------------------------------------------------

## 6. Yêu cầu phi chức năng

### NFR-001 -- Unicode

-   Toàn bộ input/output path và metadata phải hỗ trợ Unicode.
-   Hỗ trợ tiếng Việt có dấu trên Windows Terminal, PowerShell, Windows
    Console hiện đại, Linux terminal và macOS Terminal.
-   Thiết lập UTF-8 console khi phù hợp với nền tảng; không làm crash
    nếu terminal không hỗ trợ.
-   Kiểm thử đường dẫn có dấu tiếng Việt và khoảng trắng.

### NFR-002 -- Tính toàn vẹn dữ liệu

-   Không được âm thầm bỏ metadata không liên quan.
-   Không được thay đổi audio stream.
-   Có backup trước khi ghi.
-   Có preview và xác nhận.
-   Mỗi file được xử lý độc lập và có kết quả rõ ràng.

### NFR-003 -- Hiệu năng

-   Có thể xử lý thư mục vài nghìn file MP3 mà không tải toàn bộ file
    audio vào RAM cùng lúc.
-   Đọc metadata theo từng file hoặc theo batch có giới hạn.
-   UI không bị đóng băng trong lúc xử lý.
-   Hiển thị tiến độ với batch đủ lớn.

### NFR-004 -- Khả năng sử dụng

-   Các lựa chọn và lỗi phải dễ hiểu với người không chuyên.
-   Có thể quay lại menu trước.
-   Có thông báo xác nhận trước thao tác ghi.
-   Màu sắc có độ tương phản hợp lý; không chỉ dùng màu để biểu đạt
    trạng thái.
-   Không yêu cầu người dùng hiểu ID3 frame.

### NFR-005 -- Tương thích

-   Target .NET 10.
-   Hỗ trợ Windows, Linux và macOS nếu thư viện và thao tác filesystem
    cho phép.
-   Không hard-code path separator hoặc đường dẫn kiểu Windows.
-   CI/build phải có thể chạy trên môi trường không có terminal tương
    tác.

### NFR-006 -- Bảo trì

-   Tách UI, business logic và filesystem/tag implementation.
-   Có unit test cho logic mapping, validation, selection và batch
    update.
-   Có integration test trên fixture MP3 hợp lệ.
-   Không đặt logic nghiệp vụ trong CLI prompt handler.
-   Không để domain/core phụ thuộc thư viện UI.

### NFR-007 -- Bảo mật và quyền truy cập

-   Không tự động truy cập mạng.
-   Không thực thi file được quét.
-   Không xóa file MP3.
-   Từ chối hoặc xử lý an toàn path traversal khi restore manifest.
-   Validate manifest trước khi sử dụng.
-   Không theo symbolic link/reparse point một cách không kiểm soát.

------------------------------------------------------------------------

## 7. Quy tắc nghiệp vụ và edge cases

1.  **File không có tag:** hiển thị trường là trống; cho phép tạo tag.
2.  **File có tag lỗi/corrupt:** đánh dấu lỗi, không ghi đè nếu không
    thể đọc an toàn.
3.  **File read-only:** báo lỗi cụ thể; tiếp tục file khác.
4.  **File đang được process khác sử dụng:** bắt lỗi I/O, báo lỗi và
    tiếp tục.
5.  **File bị xóa sau khi scan:** đánh dấu skipped hoặc failed, không
    crash.
6.  **Trùng tên file ở thư mục con:** dùng relative path để định danh,
    không dùng filename đơn lẻ.
7.  **Tên file có Unicode:** giữ nguyên path và hiển thị đúng.
8.  **Giá trị không đổi:** không ghi file, đánh dấu unchanged.
9.  **Giá trị rỗng:** chỉ xóa khi người dùng chọn Clear.
10. **Nhiều file có metadata khác nhau:** preview phải thể hiện sự khác
    biệt; batch update chỉ thay trường được yêu cầu.
11. **Restore manifest không hợp lệ:** từ chối với thông báo; không thực
    hiện một phần nếu manifest chưa được validate.
12. **Restore path không còn an toàn:** chuẩn hóa và xác minh file đích
    nằm trong root được phép; không theo đường dẫn tuyệt đối tùy tiện
    trong manifest.
13. **Lỗi giữa batch:** tiếp tục các file còn lại; manifest ghi trạng
    thái từng file.
14. **File có cả ID3v1 và ID3v2:** xác định rõ thư viện được chọn xử lý
    đồng bộ hay ưu tiên tag nào; không để hai tag mâu thuẫn nếu có thể
    tránh.
15. **Ảnh bìa hoặc frame không hỗ trợ:** không xóa trong quá trình chỉnh
    text metadata.

------------------------------------------------------------------------

## 8. Định hướng kiến trúc

### 8.1. Project structure

``` text
MusicTagger/
├── src/
│   ├── MusicTagger.Cli/
│   │   ├── Menus/
│   │   ├── Prompts/
│   │   ├── Presenters/
│   │   └── Program.cs
│   ├── MusicTagger.Core/
│   │   ├── Models/
│   │   ├── Contracts/
│   │   ├── Services/
│   │   └── Validation/
│   └── MusicTagger.Infrastructure/
│       ├── AudioTags/
│       ├── FileSystem/
│       ├── Backup/
│       ├── Configuration/
│       └── Logging/
├── tests/
│   ├── MusicTagger.Core.Tests/
│   └── MusicTagger.IntegrationTests/
├── samples/
├── README.md
└── MusicTagger.sln
```

Đây là định hướng, không phải yêu cầu phải tạo đúng từng folder nếu
repository hiện có quy ước tốt hơn.

### 8.2. Trách nhiệm

-   **CLI:** điều hướng menu, nhập liệu, hiển thị bảng, progress và
    confirmation.
-   **Core:** model metadata, edit operation, validation, preview, batch
    orchestration và use case.
-   **Infrastructure:** đọc/ghi ID3, duyệt filesystem, backup/restore,
    settings và logging.

### 8.3. Interface định hướng

``` csharp
public interface IAudioMetadataReader
{
    Task<AudioMetadataReadResult> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public interface IAudioMetadataWriter
{
    Task<MetadataWriteResult> WriteAsync(
        string filePath,
        MetadataUpdate update,
        Id3Version targetVersion,
        CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<BackupManifest> CreateBackupAsync(
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreAsync(
        string manifestPath,
        CancellationToken cancellationToken = default);
}
```

Agent có thể điều chỉnh chữ ký interface cho phù hợp thiết kế thực tế.
Không tạo abstraction dư thừa nếu chưa có nhu cầu.

### 8.4. Thư viện

Có thể đánh giá: - **Spectre.Console:** terminal UI, prompts, tables,
progress, status. - **ATL** hoặc **TagLib#**: đọc/ghi metadata audio.

AI agent phải kiểm tra trạng thái duy trì, license, API và khả năng
tương thích .NET 10 của thư viện tại thời điểm triển khai. Chọn một thư
viện metadata chính, không thêm nhiều thư viện trùng chức năng. Ghi rõ
quyết định và trade-off trong README hoặc ADR.

------------------------------------------------------------------------

## 9. Kiểm thử và tiêu chí nghiệm thu

### 9.1. Functional acceptance criteria

-   [ ] App khởi chạy và hiển thị menu chính.
-   [ ] Nhập/paste được folder path có dấu cách và tiếng Việt.
-   [ ] Quét được file `.mp3` không phân biệt hoa thường.
-   [ ] Có tùy chọn scan đệ quy.
-   [ ] Hiển thị danh sách file và metadata hiện tại.
-   [ ] Chọn được một file, nhiều file và tất cả file.
-   [ ] Sửa được Title, Artist, Album.
-   [ ] Batch update chỉ thay trường được chọn.
-   [ ] Có thao tác Clear rõ ràng, phân biệt với Keep.
-   [ ] Có preview old/new trước khi ghi.
-   [ ] Có xác nhận trước khi ghi.
-   [ ] Tạo backup metadata trước khi sửa.
-   [ ] Ghi tag và đọc lại để verify.
-   [ ] Có progress và summary thành công/thất bại/bỏ qua.
-   [ ] Restore được metadata từ backup.
-   [ ] Lỗi một file không làm dừng toàn bộ batch.
-   [ ] Không thay đổi audio stream.
-   [ ] Không làm mất các frame metadata không liên quan trong các test
    fixture được hỗ trợ.

### 9.2. Unicode test cases

Phải có test ít nhất các giá trị sau:

``` text
Title:  Đường Đến Ngày Vinh Quang
Artist: Trịnh Công Sơn
Album:  Những Bản Tình Ca
Comment: Bài hát tiếng Việt – thử nghiệm Unicode: ă â ê ô ơ ư đ
```

Kiểm tra: 1. Nhập trên console. 2. Preview hiển thị đúng. 3. Ghi vào
MP3. 4. Đọc lại bằng chính app. 5. Đọc bằng một công cụ metadata độc lập
nếu môi trường CI cho phép. 6. Xác nhận không có ký tự `�`, mojibake
hoặc mất dấu.

### 9.3. Data integrity test cases

-   So sánh audio stream/hash trước và sau khi thay metadata bằng
    fixture và công cụ phù hợp.
-   Đảm bảo backup manifest có metadata cũ.
-   Mô phỏng lỗi ghi và đảm bảo file gốc không bị mất.
-   Restore và so sánh metadata với trạng thái ban đầu.
-   Đảm bảo file unchanged không bị ghi lại không cần thiết.

### 9.4. Definition of Done

Một hạng mục chỉ được coi là hoàn thành khi: - Code build thành công. -
Unit/integration tests liên quan pass. - Có xử lý lỗi và cancellation
hợp lý. - Có cập nhật README nếu ảnh hưởng cách sử dụng. - Không có TODO
quan trọng hoặc stub giả lập ở luồng MVP. - Có thể chạy app theo hướng
dẫn trên môi trường sạch.

------------------------------------------------------------------------

## 10. Phân kỳ triển khai

### Phase 1 -- MVP bắt buộc

-   Menu console.
-   Nhập folder path.
-   Scan MP3 (non-recursive mặc định; recursive là tùy chọn).
-   Đọc Title, Artist, Album.
-   Chọn single / multi / all.
-   Chỉnh metadata đơn và batch.
-   Preview, confirmation.
-   Backup metadata JSON.
-   Write + read-back verification.
-   Progress và summary.
-   Unicode tests.
-   README.

### Phase 2 -- Tăng tiện ích

-   Các trường Genre, Year, Track Number, Disc Number, Composer,
    Comment.
-   Restore UI hoàn chỉnh.
-   Settings.
-   Filename parser theo pattern.
-   Auto numbering và sorting.
-   Xuất báo cáo CSV/JSON.

### Phase 3 -- Mở rộng

-   Album art.
-   Tích hợp nguồn metadata trực tuyến (chỉ khi người dùng chủ động
    bật).
-   Hỗ trợ FLAC/M4A/WAV và các định dạng khác.
-   Preset cho MP3 Player.

Không để Phase 2/3 làm chậm hoặc làm phức tạp MVP.

------------------------------------------------------------------------

## 11. Rủi ro và quyết định cần ghi nhận

  -----------------------------------------------------------------------
  Rủi ro                              Biện pháp
  ----------------------------------- -----------------------------------
  Một số MP3 Player không hiển thị    Default ID3v2.3; test encoding và
  đúng UTF-8                          ghi rõ giới hạn

  Thư viện ghi tag làm mất frame      Tạo fixture nhiều loại tag/frame;
  không hỗ trợ                        kiểm tra round-trip

  Ghi file thất bại giữa chừng        Backup trước, safe-write và xử lý
                                      từng file

  User chọn nhầm folder hoặc batch    Preview, số lượng file và
  quá rộng                            confirmation

  Path traversal trong restore        Validate manifest và containment
                                      của path

  Terminal không hỗ trợ màu           Auto-detect và fallback plain text

  API/package thay đổi                Pin package version và ghi quyết
                                      định dependency
  -----------------------------------------------------------------------

### 11.1. Giả định mặc định cho MVP

-   Chỉ xử lý MP3.
-   Không gọi mạng.
-   Quét non-recursive mặc định.
-   ID3v2.3 là write default.
-   Batch edit chỉ áp dụng các trường user chủ động chọn.
-   Backup metadata JSON, không backup audio bytes.
-   Mọi thao tác ghi đều có preview và confirmation.
-   App không tự đổi tên file.
-   Không tự suy đoán metadata từ tên file trong MVP.

------------------------------------------------------------------------

## 12. Deliverables

1.  Source code và solution có thể build.
2.  CLI app chạy được.
3.  Unit tests và integration tests.
4.  README hướng dẫn cài đặt, build, chạy, ví dụ thao tác và giới hạn
    tương thích.
5.  Mẫu cấu hình.
6.  Mẫu backup manifest.
7.  Tóm tắt kiến trúc và dependency/license.
8.  Báo cáo các acceptance criteria đã hoàn thành/chưa hoàn thành.

**Kết thúc SRS -- MusicTagger CLI v1.0**
