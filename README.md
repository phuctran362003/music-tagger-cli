# MusicTagger CLI

Công cụ console (.NET 10) để chuẩn hoá metadata cho thư viện nhạc offline: quét thư mục,
xem và sửa tag của một file / nhiều file / tất cả file, xem trước thay đổi, xác nhận, ghi an
toàn và khôi phục lại từ bản backup metadata. Hỗ trợ **mọi định dạng nhạc phổ biến** đã được
kiểm chứng: MP3, FLAC, M4A/M4B, AAC, OGG Vorbis, Opus, WAV, AIFF.

Yêu cầu gốc: [`docs/MusicTagger_SRS.md`](docs/MusicTagger_SRS.md).

Trạng thái: **MVP hoàn chỉnh và đã kiểm thử** — 202 test tự động pass
(114 unit + 58 integration + 30 CLI), build sạch 0 warning. Xem mục
[Kiểm thử](#12-kiểm-thử).

---

## 1. Mục đích

MP3 tải về thường thiếu hoặc sai tag (Title/Artist/Album/Track…). Hậu quả: player xếp nhạc vào
*Unknown*, sắp sai thứ tự, khó tìm; sửa tay bằng công cụ khác thì dễ làm rơi tag hoặc ảnh bìa.
Thư viện nhạc thật thường trộn nhiều định dạng (MP3 tải về, FLAC rip từ CD, M4A mua trên store,
WAV thu âm…) nên app xử lý tất cả trong cùng một lần quét.

MusicTagger CLI giải quyết đúng việc đó, với ba nguyên tắc:

1. **Không ghi khi chưa xem trước và xác nhận** — mọi thay đổi đi qua bước preview (giá trị cũ /
   giá trị mới, file nào bị ảnh hưởng, file nào bị bỏ qua).
2. **Có đường lùi** — trước khi ghi, metadata cũ của từng file được lưu thành manifest JSON; có
   thể restore lại bất cứ lúc nào từ menu.
3. **Không phá dữ liệu** — chỉ ghi các trường được yêu cầu, không đổi audio stream, không xoá và
   không đổi tên file nhạc, mỗi file được xử lý độc lập và đọc lại để verify.

Ngoài phạm vi: tải nhạc, tra metadata online, đổi tên file, quản lý ảnh bìa, và các định dạng
chưa kiểm chứng được (WMA/ASF, APE, WavPack, Musepack, DSF, container video) — xem
[mục 1.1](#11-định-dạng-hỗ-trợ).

### 1.1. Định dạng hỗ trợ

App chỉ nhận những định dạng đã được kiểm chứng bằng integration test (ghi thật + đọc lại verify
trên file thật). Mọi file khác bị bỏ qua khi quét, không bị ghi "mù".

| Định dạng | Extension | Tag ghi ra | Ghi chú |
|---|---|---|---|
| MP3 | `.mp3` | ID3v2.3 / ID3v2.4 (theo Settings) | ID3v1 được gộp vào ID3v2 rồi xoá |
| FLAC | `.flac` | Vorbis comment (UTF-8) | |
| M4A / M4B | `.m4a`, `.m4b` | MP4/iTunes atom (UTF-8) | |
| AAC (ADTS) | `.aac` | ID3v2.3 / ID3v2.4 | |
| OGG Vorbis | `.ogg`, `.oga` | Vorbis comment (UTF-8) | |
| Opus | `.opus` | Vorbis comment (UTF-8) | |
| WAV | `.wav` | ID3v2.4 trong chunk `id3 ` | Luôn v2.4, xem [mục 7](#7-tag-system-và-encoding) |
| AIFF | `.aiff`, `.aif` | ID3v2.3 / ID3v2.4 | |

Không hỗ trợ (bị bỏ qua khi quét): `.wma`, `.ape`, `.wv`, `.mpc`, `.dsf`, `.m4v`/`.mp4` (video),
`.wave` (thư viện tag không mở được container này).

**Khác biệt so với SRS:** [`docs/MusicTagger_SRS.md`](docs/MusicTagger_SRS.md) xếp "quản lý mọi định
dạng audio ngoài MP3" vào *out of scope* và để FLAC/M4A/WAV ở Phase 2. Theo yêu cầu của chủ dự án,
bản này mở rộng phạm vi sang toàn bộ định dạng trong bảng trên. File SRS được giữ nguyên làm tài
liệu gốc; phần lệch chuẩn được ghi ở đây và ở [mục 14](#14-giới-hạn-đã-biết).

---

## 2. Yêu cầu môi trường

| Thành phần | Yêu cầu |
|---|---|
| .NET SDK | **10.0** trở lên (đã build và test bằng SDK `10.0.404`, runtime `10.0.15`) |
| Hệ điều hành | Windows, Linux, macOS (không hard-code đường dẫn kiểu Windows) |
| Terminal | Bất kỳ; app tự chuyển UTF-8 để hiển thị tiếng Việt, có fallback không màu |
| Mạng | **Không cần** — app không truy cập mạng ở runtime |

Kiểm tra SDK:

```bash
dotnet --version   # phải in ra 10.x
```

---

## 3. Build, chạy, test

```bash
# Build toàn bộ solution
dotnet build MusicTaggerCli.sln

# Chạy app (từ thư mục gốc repo)
dotnet run --project src/MusicTagger.Cli

# Chạy bản đã build
dotnet src/MusicTagger.Cli/bin/Debug/net10.0/MusicTagger.Cli.dll

# Chạy toàn bộ test
dotnet test MusicTaggerCli.sln

# Chạy riêng từng nhóm test
dotnet test tests/MusicTagger.Core.Tests
dotnet test tests/MusicTagger.IntegrationTests
dotnet test tests/MusicTagger.Cli.Tests
```

Tham số dòng lệnh:

| Tham số | Ý nghĩa |
|---|---|
| `-h`, `--help` | In hướng dẫn và thoát |
| `-v`, `--version` | In phiên bản và thoát |
| `--no-color` | Tắt màu cho riêng lần chạy này (không lưu vào settings) |

App cũng chạy được khi input/output bị pipe (CI, script): không có prompt tương tác nào bị treo,
progress bar tự chuyển thành dòng chữ `Done: n/n`.

### 3.1. Publish ra một file `.exe`

Dùng script có sẵn (tự publish, kiểm tra file sinh ra và chạy thử `--version`):

```powershell
# Khuyến nghị: 1 file .exe duy nhất, tự chứa runtime .NET (~72 MB)
.\publish.ps1

# Bản nhỏ (~2 MB) nhưng máy đích phải có .NET 10 runtime
.\publish.ps1 -Mode framework-dependent

# Tuỳ chọn khác
.\publish.ps1 -Runtime win-arm64 -Version 1.2.0 -Output C:\apps\musictagger
```

Kết quả:

| Mode | File sinh ra | Kích thước | Máy đích cần gì |
|---|---|---|---|
| `self-contained` (mặc định) | `publish\win-x64-self-contained\MusicTagger.Cli.exe` | ~72 MB | không cần cài .NET |
| `framework-dependent` | `publish\win-x64-fx\MusicTagger.Cli.exe` | ~2 MB | .NET 10 Desktop Runtime |

Cả hai đều là **single-file**: thư mục `publish\` chỉ có đúng một file `.exe`, copy đi đâu cũng
chạy, không cần `dotnet`. Nếu không muốn dùng script, lệnh tương đương là:

```powershell
dotnet publish src/MusicTagger.Cli/MusicTagger.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish\win-x64
```

Lưu ý khi phân phối:

- Đổi tên file `.exe` tuỳ ý (`musictagger.exe`…) — app không phụ thuộc tên file.
- Settings, log và backup **không** nằm cạnh `.exe` mà ở `%APPDATA%\MusicTagger\` (mục 8, 9).
- Double-click là chạy, nhưng cửa sổ console sẽ đóng ngay khi thoát app; nếu muốn đọc lại thông
  báo cuối, chạy từ `cmd`/PowerShell hoặc tạo shortcut.
- Bản `self-contained` chạy được trên Windows 10/11 x64 (hoặc ARM64 với `-Runtime win-arm64`);
  muốn chạy trên Linux/macOS thì publish với `-r linux-x64` / `-r osx-arm64` (`-Mode framework-dependent`
  cần runtime tương ứng trên máy đích).
- Đã kiểm chứng bản publish: `MusicTagger.Cli.exe --version` chạy đúng, và một lượt quét → sửa →
  ghi thật trên folder trộn MP3 + WAV + FLAC ghi đúng tag (ID3v2.3/UTF-16, WAV `id3 ` v2.4, Vorbis
  comment UTF-8), giữ nguyên audio payload.

---

## 4. Hướng dẫn sử dụng từng bước

### 4.1. Menu chính

```text
╭──────────────────────────────────────────────────────────╮
│ MusicTagger CLI  v1.0.0                                  │
│ Organize your offline music library                      │
╰──────────────────────────────────────────────────────────╯

Select an option  arrows, then Enter
> Scan and edit music folder
  Restore metadata backup
  Settings
  Exit
```

Dùng phím mũi tên để di chuyển và Enter để chọn. Khi input bị pipe (CI, script), menu trở lại
dạng nhập số `1`–`4`. Ở prompt nhập chữ, `b` = quay lại (không thay đổi gì). Ctrl+C huỷ thao tác
đang chạy một cách an toàn.

### 4.2. Quét thư mục (menu 1)

```text
Music folder path: D:\Music\Nhạc Trịnh
Scan sub folders too? (setting: Off) [y/N]
Scanning 'D:\Music\Nhạc Trịnh' ...
Reading metadata of 24 file(s)
Done: 24/24
Success: Found 24 music file(s) in 'D:\Music\Nhạc Trịnh'.
```

- Dán đường dẫn trực tiếp được; dấu nháy bao quanh (`"D:\Music"`) được tự bỏ.
- Đường dẫn có dấu cách và tiếng Việt được hỗ trợ.
- Nhận mọi định dạng trong [mục 1.1](#11-định-dạng-hỗ-trợ) (không phân biệt hoa/thường); file
  khác bị bỏ qua. Không đi theo symbolic link/reparse point.
- Mặc định **không** quét thư mục con; trả lời `y` để quét đệ quy. Giá trị mặc định này lấy từ
  Settings.
- Không tìm thấy file nào → app báo rõ (kèm danh sách định dạng hỗ trợ) và quay về menu, không
  ghi gì.

### 4.3. Xem danh sách và chọn file

```text
╭─┬──────────────────────────────┬──────────────┬──────────────┬─────────┬──────┬───────────────┬───────╮
│#│File                          │Title         │Artist        │Album    │Type  │Tag            │   Size│
├─┼──────────────────────────────┼──────────────┼──────────────┼─────────┼──────┼───────────────┼───────┤
│1│01 - Thuong ve mien Trung.mp3 │-             │Trịnh Công Sơn│Sơn Ca 7 │MP3   │ID3v2.3        │12.2 KB│
│2│02 - Nho mua pho Hue.flac     │-             │Trịnh Công Sơn│Sơn Ca 7 │FLAC  │Vorbis comment │12.2 KB│
│3│03 - Bien nho.wav             │-             │Trịnh Công Sơn│Sơn Ca 7 │WAV   │none           │ 2.1 MB│
╰─┴──────────────────────────────┴──────────────┴──────────────┴─────────┴──────┴───────────────┴───────╯

Choose an operation
Arrow keys to move, Enter to choose.
> Edit one file
  Select multiple files
  Edit all files
  Back
```

- Cột `Type` = định dạng file, `Tag` = tag hệ nào đang thực sự có trên đĩa (`none` nếu chưa có
  tag, `Vorbis comment`, `MP4 atoms`, `ID3v2.3`, `RIFF INFO`…).
- Trường trống hiển thị `-` (không tự điền giá trị mặc định vào file).
- File lỗi/không đọc được hiển thị `read error` và không được đưa vào danh sách ghi.
- `Edit one file`: nhập số thứ tự file → xem toàn bộ trường hiện tại rồi sửa từng trường.
- `Select multiple files`: nhập danh sách số (`1,3` hoặc `1 4`), hoặc `a` = tất cả.
- `Edit all files`: áp dụng cho mọi file trong lần quét.

### 4.4. Sửa metadata: Keep / Set / Clear

Mỗi trường luôn có ba trạng thái rõ ràng — **giá trị rỗng không bao giờ có nghĩa là xoá**:

```text
Choose the fields to update
  1. Title ...  2. Artist ...  3. Album ...  (a = all, b = back)
Choose the fields to update 5

Genre - current (empty): keep / set / clear? s
Genre value: Nhạc trữ tình
```

- `Edit all files` / `Select multiple files`: chọn trước các trường cần sửa, sau đó quyết định
  `keep` (giữ nguyên) / `set` (nhập giá trị mới) / `clear` (xoá hẳn field) cho từng trường.
- `Edit one file`: đi qua từng trường với giá trị hiện tại; **Enter = keep**.
- Validate ngay khi nhập: Year phải là số 1000–2999, Track/Disc dạng `3` hoặc `3/12`, giới hạn
  512 ký tự (Comment 2048), chặn ký tự điều khiển.

### 4.5. Preview (dry run)

```text
PREVIEW - no files have been changed yet

╭─────────────────────────┬─────╮
│Item                     │Count│
├─────────────────────────┼─────┤
│Files selected           │    3│
│Files with changes       │    3│
│Unchanged (nothing to do)│    0│
│Skipped (missing)        │    0│
│Failed (cannot write)    │    0│
╰─────────────────────────┴─────╯

Field changes
╭──────┬─────────┬──────────────┬─────╮
│Field │Old value│New value     │Files│
├──────┼─────────┼──────────────┼─────┤
│Artist│-        │Trịnh Công Sơn│    3│
╰──────┴─────────┴──────────────┴─────╯

Files that will be updated
  ... (mỗi file: các thay đổi cụ thể)

Apply these changes to 3 of 3 file(s)? [y/N]
```

Bước này **không ghi gì cả**. Nó cho biết: bao nhiêu file sẽ thay đổi, thay đổi gì (cũ → mới),
file nào bị bỏ qua/không ghi được và lý do.

### 4.6. Xác nhận và ghi

- Mặc định là `[y/N]` — **Enter là không ghi**, phải gõ `y` mới ghi.
- Trước khi ghi, app tạo manifest backup metadata cũ. Nếu backup thất bại thì **không file nào bị
  sửa**.
- Trong lúc ghi có progress (`Writing metadata to 3 file(s)`), terminal tương tác hiển thị thanh
  tiến trình; terminal/pipe hiển thị `Done: n/n`.

### 4.7. Kết quả

```text
Completed
╭─────────┬─────╮
│Status   │Files│
├─────────┼─────┤
│Updated  │    2│
│Failed   │    1│
│Skipped  │    0│
│Unchanged│    0│
│Total    │    3│
╰─────────┴─────╯
Tag format used: ID3v2.3 / Vorbis comment / MP4 atoms
ID3 write version applies to: MP3, AAC, AIFF
Backup manifest:
  C:\Users\<user>\AppData\Roaming\MusicTagger\backups\musictagger-edit-all-....json

Failed files
╭────────────────────┬──────────────────╮
│File                │Reason            │
├────────────────────┼──────────────────┤
│03 - Track.mp3      │The file is read-only.│
╰────────────────────┴──────────────────╯
```

Trạng thái luôn được ghi bằng **chữ** (Updated / Failed / Skipped / Unchanged), không chỉ bằng
màu. Sau khi ghi, danh sách file được đọc lại từ đĩa để hiển thị đúng giá trị đã lưu.

### 4.8. Restore (menu 2)

```text
Backup location: C:\Users\<user>\AppData\Roaming\MusicTagger\backups
╭─┬───────────────────┬─────────┬─────╮
│#│Created            │Operation│Files│
├─┼───────────────────┼─────────┼─────┤
│1│2026-05-01 09:12:44│edit-all │    3│
╰─┴───────────────────┴─────────┴─────╯
Choose a backup to restore
```

Chọn backup → app hiển thị **restore preview** (file nào, trường nào, cũ → mới; file thiếu bị
`Skipped`) → xác nhận `[y/N]` → restore. Trước khi restore, trạng thái hiện tại lại được backup
một lần nữa, nên có thể "undo" bằng cách restore ngược lại.

Nếu thư mục gốc trong manifest không còn (đổi tên/ổ đĩa khác), app yêu cầu nhập thư mục mới; nếu
thư mục mới nằm ngoài root ban đầu thì phải xác nhận rõ ràng lần nữa.

### 4.9. Settings (menu 3)

| Thiết lập | Giá trị | Mặc định |
|---|---|---|
| ID3 write version | ID3v2.3 (UTF-16) / ID3v2.4 (UTF-8) | ID3v2.3 |
| Recursive scan | Off / On | Off |
| Backup directory | mặc định hoặc thư mục tự chọn (tự tạo nếu chưa có) | thư mục dữ liệu app |
| Console color | Auto / On / Off | Auto |

ID3 write version chỉ ảnh hưởng tới các container dùng tag ID3 (**MP3, AAC, AIFF**); FLAC/OGG/Opus
luôn dùng Vorbis comment UTF-8, M4A/M4B luôn dùng MP4 atom UTF-8, và **WAV luôn lưu ID3v2.4**
(giới hạn của thư viện tag — xem [mục 7](#7-tag-system-và-encoding)).

Thay đổi được lưu ngay vào JSON. Riêng "Console color" áp dụng từ lần chạy sau.
Ví dụ file settings hoàn chỉnh: [`samples/settings.json`](samples/settings.json).

---

## 5. Ví dụ batch

Chuẩn hoá album "Sơn Ca 7" gồm 24 file: gán Artist + Album cho toàn bộ, đặt Year và gán Track
theo thứ tự file.

```text
1                                   # Scan and edit
D:\Music\Nhạc Trịnh\Sơn Ca 7        # thư mục
n                                   # không quét thư mục con
3                                   # Edit all files  (24 file)
2,3                                 # chọn trường Artist và Album
s                                   # Artist: set
Trịnh Công Sơn
s                                   # Album: set
Sơn Ca 7
y                                   # xem preview xong, xác nhận ghi
b                                   # về menu chính
4                                   # thoát
```

Kết quả: chỉ Artist và Album bị thay đổi trên 24 file; các trường khác (Title, Genre, ảnh bìa…)
giữ nguyên; một manifest backup được tạo trước khi ghi.

Kịch bản tương tự có thể chạy tự động (dùng cho CI/kiểm thử hồi quy) bằng cách pipe input:

```bash
printf '1\nD:\\Music\\Nhạc Trịnh\\Sơn Ca 7\nn\n3\n2,3\ns\nTrịnh Công Sơn\ns\nSơn Ca 7\ny\nb\n4\n' \
  | dotnet src/MusicTagger.Cli/bin/Debug/net10.0/MusicTagger.Cli.dll
```

Xoá một trường trên nhiều file (ví dụ bỏ Comment sai): chọn trường `10` rồi `c` (clear).

---

## 6. Trường metadata hỗ trợ

Mười trường dưới đây được đọc/ghi cho **mọi định dạng**. Cột "ID3v2 frame" chỉ ra frame tương ứng
khi container dùng ID3; FLAC/OGG/Opus ghi cùng dữ liệu vào Vorbis comment (`TITLE`, `ARTIST`,
`ALBUM`, `ALBUMARTIST`, `GENRE`, `DATE`, `TRACKNUMBER`, `DISCNUMBER`, `COMPOSER`, `COMMENT`),
M4A/M4B ghi vào atom `©nam`, `©ART`, `©alb`, `aART`, `©gen`, `©day`, `trkn`, `disk`, `©wrt`, `©cmt`.

| Trường | ID3v2 frame | Kiểu giá trị | Ghi chú |
|---|---|---|---|
| Title | `TIT2` | text | |
| Artist | `TPE1` | text, nhiều giá trị ngăn cách `;` | |
| Album | `TALB` | text | |
| Album artist | `TPE2` | text, nhiều giá trị ngăn cách `;` | |
| Genre | `TCON` | text, nhiều giá trị ngăn cách `;` | |
| Year | `TDRC` (v2.4) / `TYER` (v2.3) | số 1000–2999 | thư viện tự chọn frame theo version |
| Track number | `TRCK` | `3` hoặc `3/12` | |
| Disc number | `TPOS` | `1` hoặc `1/2` | |
| Composer | `TCOM` | text, nhiều giá trị ngăn cách `;` | |
| Comment | `COMM` | text (cho phép xuống dòng) | |

Ngoài các trường trên, **không** dữ liệu nào khác bị sửa: ảnh bìa (`APIC`/`covr`/`METADATA_BLOCK
PICTURE`), lời bài hát, replay gain, frame lạ… được giữ nguyên (có test chứng minh APIC còn nguyên
sau khi ghi MP3).

---

## 7. Tag system và encoding

Mỗi container ghi metadata theo một hệ tag riêng; app không "ép" mọi file về ID3:

| Định dạng | Hệ tag ghi ra | Encoding | Chịu ảnh hưởng của setting ID3 version? |
|---|---|---|---|
| MP3, AAC (ADTS) | ID3v2.3 / ID3v2.4 | UTF-16 / UTF-8 | **Có** |
| AIFF (`.aiff`, `.aif`) | ID3v2.3 / ID3v2.4 trong chunk `ID3 ` | UTF-16 / UTF-8 | **Có** |
| WAV | ID3v2.4 trong chunk `id3 ` | UTF-8 | **Không** — luôn v2.4 |
| FLAC, OGG Vorbis, Opus | Vorbis comment | UTF-8 | Không (luôn UTF-8) |
| M4A, M4B | MP4/iTunes atom | UTF-8 | Không (luôn UTF-8) |

Với hai mức ID3 mà app ghi cho MP3/AAC/AIFF:

| Ghi ra | Encoding của frame text | Khi nào dùng |
|---|---|---|
| **ID3v2.3** (mặc định) | UTF-16 (có BOM, encoding byte `0x01`) | Tương thích tốt nhất với MP3 player cũ, thiết bị ô tô |
| ID3v2.4 | UTF-8 (encoding byte `0x03`) | Thiết bị/phần mềm hiện đại, file nhỏ hơn |

Chi tiết đã kiểm chứng bằng test ở mức byte:

- Ghi v2.3 → header `ID3 03 00` (byte thứ 4 của file MP3/AAC = `03`), frame text có encoding byte
  `01` + BOM `FF FE`, ký tự tiếng Việt được lưu đúng (ví dụ `ị` = `CB 1E` trong UTF-16LE). Không
  bao giờ ghi UTF-8 vào frame v2.3.
- Ghi v2.4 → header `ID3 04 00`, frame mới được ghi bằng UTF-8 (`03`).
- WAV: chunk `id3 ` chứa ID3v2.4 **bất kể** setting (đo trực tiếp byte version trong chunk với
  TagLib# 2.3.0). App nói rõ điều này trong màn hình Settings thay vì hứa v2.3.
- FLAC/OGG/Opus/M4A: giá trị được đọc lại nguyên vẹn tiếng Việt, không phụ thuộc setting.

**Lưu ý quan trọng về tương thích thiết bị cũ:** đọc lại thành công trong app chỉ chứng minh các
byte đã được lưu đúng chuẩn. Nó **không** bảo đảm mọi thiết bị sẽ hiển thị tiếng Việt giống nhau —
một số đầu đĩa/ô tô đời cũ chỉ đọc được ISO-8859-1 và sẽ hiện sai dấu. Nếu gặp trường hợp đó, hãy
dùng ID3v2.3 (mặc định) và kiểm tra trên chính thiết bị.

Ghi chú thêm:

- Frame **không bị sửa** giữ nguyên encoding cũ của nó (một file v2.4 vẫn có thể còn frame UTF-16
  hợp lệ); chỉ frame được ghi lại mới dùng encoding của version đích.
- File chỉ có **ID3v1**: khi ghi, ID3v1 được hợp nhất vào ID3v2 rồi xoá, để không tồn tại hai tag
  mâu thuẫn nhau.
- File **ID3v2.2** sẽ được nâng lên version đích khi ghi (thư viện không ghi được v2.2).
- **WAV/AIFF có tag RIFF INFO** (do tool khác tạo, ví dụ `LIST/INFO` hoặc `IDVX`): khi ghi, các giá
  trị đó được chuyển vào ID3v2 và chunk cũ được bỏ, để file chỉ còn một tag duy nhất và bước verify
  đọc lại đúng thứ vừa ghi. Giá trị cũ vẫn nằm trong manifest backup nên restore được.

---

## 8. Backup và Restore

### 8.1. Vị trí

| Nền tảng | Thư mục dữ liệu ứng dụng |
|---|---|
| Windows | `%APPDATA%\MusicTagger\` (Roaming) |
| Linux | `$XDG_CONFIG_HOME/MusicTagger/` hoặc `~/.config/MusicTagger/` |
| macOS | `~/.config/MusicTagger/` (theo `SpecialFolder.ApplicationData`) |

```text
<app data>/MusicTagger/
├── settings.json                                  # thiết lập
├── backups/
│   └── musictagger-edit-all-20260501-091244-1a2b3c4d.json
└── logs/
    └── musictagger-20260501.log                   # log chẩn đoán theo ngày
```

Backup **không** được ghi vào thư mục nhạc (trừ khi người dùng tự chọn thư mục khác trong
Settings). Audio không bao giờ được copy vào JSON — manifest chỉ chứa đường dẫn tương đối và
metadata.

### 8.2. Nội dung manifest

```json
{
  "manifestId": "1a2b3c4d5e6f4711a1b2c3d4e5f60718",
  "rootFolder": "D:\\Music\\Nhạc Trịnh",
  "operation": "edit-all",
  "createdUtc": "2026-05-01T02:12:44.1234567+00:00",
  "appVersion": "1.0.0",
  "manifestPath": "C:\\Users\\<user>\\AppData\\Roaming\\MusicTagger\\backups\\musictagger-edit-all-20260501-091244-1a2b3c4d.json",
  "entries": [
    {
      "relativePath": "01 - Thuong ve mien Trung.mp3",
      "metadata": {
        "title": null,
        "artist": "Trịnh Công Sơn",
        "album": "Sơn Ca 7",
        "albumArtist": null,
        "genre": null,
        "year": null,
        "trackNumber": null,
        "discNumber": null,
        "composer": null,
        "comment": null
      },
      "tagFormat": "id3v23",
      "status": "updated",
      "processedUtc": "2026-05-01T02:12:45.9876543+00:00",
      "message": null
    }
  ]
}
```

`tagFormat` ghi lại tag hệ **thực sự có trên đĩa** lúc backup (`none`, `id3v1`, `id3v22`,
`id3v23`, `id3v24`, `vorbisComment`, `mp4Atom`, `riffInfo`), để restore biết file gốc dùng hệ tag
nào. `status` nhận: `captured` (chưa ghi), `updated`, `unchanged`, `skipped`, `failed`. Manifest
được cập nhật lại sau khi batch kết thúc, nên luôn phản ánh đúng file nào đã bị sửa.

Ví dụ đầy đủ: [`samples/backup-manifest.example.json`](samples/backup-manifest.example.json).

### 8.3. An toàn khi restore

- Manifest được **validate toàn bộ** trước khi dùng: thiếu id/root/operation, path tuyệt đối,
  entry trùng lặp, path thoát khỏi root (`..`) → từ chối cả manifest, không restore một phần.
- Mỗi đường dẫn trong manifest được resolve trong root đã xác nhận; file ngoài root không bị chạm
  tới (trừ khi người dùng xác nhận rõ ràng khi đổi thư mục gốc).
- File không còn tồn tại → `Skipped`, batch vẫn tiếp tục.

---

## 9. Log

- Log chẩn đoán ghi theo ngày tại `<app data>/MusicTagger/logs/musictagger-yyyyMMdd.log`.
- Log ghi đường dẫn file + exception detail; UI chỉ hiển thị thông báo dễ hiểu.
- Không log nội dung âm thanh, không log dữ liệu nhạy cảm.
- Không ghi được log (thư mục read-only) thì app vẫn chạy bình thường.

---

## 10. Kiến trúc

```text
src/
├── MusicTagger.Cli/            # UI console: menu, prompt, bảng, progress (Spectre.Console)
│   ├── Flows/                  #   MainMenuFlow, ScanEditFlow, RestoreFlow, SettingsFlow
│   ├── Presentation/           #   AppPresenter, ConsoleInput, ProgressReporter, ConsoleFactory
│   ├── Session/                #   EditSession, FolderEntry
│   ├── CliContext.cs           #   composition root
│   └── Program.cs
├── MusicTagger.Core/           # nghiệp vụ, KHÔNG phụ thuộc UI hay thư viện tag
│   ├── Models/                 #   AudioMetadata, MetadataUpdate, FieldEdit, AudioFormatCatalog,
│   │                           #   TagFormat, preview/batch/backup
│   ├── Contracts/              #   IAudioMetadataReader/Writer, IAudioFileScanner, IBackupService…
│   ├── Services/               #   UpdatePlanner, BatchUpdateExecutor, RestoreService
│   ├── Validation/             #   MetadataValidator, FolderPathValidator, ManifestValidator, PathSafety
│   └── Errors/                 #   UserFriendlyErrors, InvalidManifestException
└── MusicTagger.Infrastructure/  # triển khai cụ thể
    ├── AudioTags/              #   TagLibAudioMetadataReader/Writer, TagLibMapping, TagLibGate
    ├── FileSystem/             #   AudioFileScanner, PhysicalFileProbe
    ├── Backup/                 #   JsonBackupService
    ├── Configuration/          #   AppPaths, JsonSettingsStore
    └── Logging/                #   FileLogger, NullAppLog
```

Luồng của một lần sửa: `ScanEditFlow` → `AudioFileScanner` + `IAudioMetadataReader` (hiển thị) →
`MetadataUpdate` (Keep/Set/Clear) → `UpdatePlanner` (preview, không ghi) → xác nhận →
`BatchUpdateExecutor` (backup manifest → ghi từng file → đọc lại verify → cập nhật manifest) →
`AppPresenter` (tổng kết).

Danh sách định dạng nằm ở **một chỗ duy nhất** (`AudioFormatCatalog` trong Core): scanner, writer,
UI và test đều đọc từ đó, nên không thể có chuyện UI cho chọn một định dạng mà writer không ghi
được.

Phụ thuộc một chiều: `Cli` → `Core` ← `Infrastructure`. `Core` không tham chiếu Spectre.Console
hay TagLib#.

---

## 11. Cam kết an toàn dữ liệu

| Nguyên tắc | Cách thực hiện |
|---|---|
| Không mất dữ liệu khi ghi lỗi | Ghi vào file tạm cùng thư mục, giữ nguyên extension gốc (`.<tên>.mttmp-<guid>.<ext>`), verify, rồi thay thế nguyên tử (`File.Replace`, fallback `File.Move(overwrite)`); lỗi giữa chừng thì file gốc còn nguyên |
| Không đổi audio stream | TagLib chỉ sửa phần tag; test so sánh hash của phần audio trước/sau khi ghi, và kiểm tra payload audio còn nguyên trong từng container |
| Không xoá/đổi tên file nhạc | App không có thao tác xoá/rename; file tạm bị xoá sau khi replace |
| Chỉ sửa trường được yêu cầu | `MetadataUpdate` chỉ chứa các edit; các tag khác (APIC, USLT, picture block…) giữ nguyên |
| Không ghi lại file không đổi | Nếu giá trị đã đúng → `Unchanged`, không ghi, không tạo backup |
| Backup trước khi ghi | Manifest được ghi và verify tồn tại trên đĩa trước file đầu tiên; backup lỗi ⇒ 0 file bị sửa |
| Verify sau khi ghi | Đọc lại từ đĩa, so sánh từng trường mong đợi; phát hiện cả trường hợp field không liên quan bị mất |
| Lỗi một file không dừng batch | Mỗi file bọc try/catch riêng, kết quả ghi vào manifest theo từng file |
| Giữ mtime/quyền file | Giữ `LastWriteTimeUtc` và `UnixFileMode` của file gốc sau khi replace |
| Không path traversal | `PathSafety` chặn path tuyệt đối/`..` khi quét và khi restore |
| Không theo symlink | Scan bỏ qua reparse point/symbolic link |

---

## 12. Kiểm thử

```bash
dotnet test MusicTaggerCli.sln
```

| Nhóm | Số test | Nội dung chính |
|---|---|---|
| `MusicTagger.Core.Tests` | 114 | validation (Year/Track/độ dài/ký tự điều khiển), Keep/Set/Clear, planner, executor (backup-fail, isolation, verify), restore, path safety, manifest validation, format registry (`AudioFormatCatalog`, `TagFormat`) |
| `MusicTagger.IntegrationTests` | 58 | ghi tag thật trên file thật: MP3 (v2.3 = UTF-16, v2.4 = UTF-8, giữ APIC, ID3v1→v2), FLAC/M4A/M4B/AAC/OGG/Opus/WAV/AIFF (round-trip tiếng Việt, clear field, audio payload còn nguyên), WAV có RIFF INFO → migrate sang ID3v2, từ chối file không hỗ trợ, batch, backup/restore, scanner đa định dạng, file mẫu trong `samples/` |
| `MusicTagger.Cli.Tests` | 30 | chạy thật các flow với input script: scan → batch edit → preview → confirm → ghi (cả folder trộn MP3 + FLAC); huỷ xác nhận; "nothing to write"; restore; quét đệ quy; prompt parsing; fallback progress |

Fixture trong test được sinh tại chỗ dưới dạng file hợp lệ (MP3 = frame MPEG-1 Layer III, FLAC =
STREAMINFO, M4A = atom `ftyp`/`moov`, WAV/AIFF = chunk `fmt `/`data` và `COMM`/`SSND`, OGG/Opus =
page Ogg + Vorbis/Opus header, AAC = frame ADTS), không dùng ffmpeg hay file mẫu tải về. Riêng MP3
còn được kiểm tra bằng bộ đọc ID3 độc lập (không dùng TagLib) để chứng minh encoding ở mức byte.

Bộ test Unicode bao gồm: `Trịnh Công Sơn`, `Những Bản Tình Ca`, `Đường Đến Ngày Vinh Quang`,
`Bài hát tiếng Việt – thử nghiệm: ă â ê ô ơ ư đ`, ký tự ngoài BMP (`①`) và chuỗi có dấu tổ hợp
(decomposed), cùng tên file/thư mục có dấu và khoảng trắng.

---

## 13. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `Error: Folder not found: ...` | Gõ sai đường dẫn, ổ chưa mount | Kiểm tra lại; dán đường dẫn từ Explorer/Finder |
| `Warning: No supported music files were found.` | Thư mục rỗng, chỉ có định dạng chưa hỗ trợ, hoặc file nằm trong thư mục con | Trả lời `y` cho câu hỏi quét thư mục con; xem danh sách định dạng ở [mục 1.1](#11-định-dạng-hỗ-trợ) |
| `Failed: The file is read-only.` | Thuộc tính read-only / quyền NTFS | Bỏ read-only hoặc chạy bằng user có quyền |
| `Failed: Access denied...` | File đang mở bởi app khác, hoặc thiếu quyền | Đóng app đang dùng file rồi thử lại |
| `The metadata backup could not be created: ...` | Thư mục backup không ghi được (đầy đĩa/quyền) | Sửa Settings → Backup directory, hoặc giải phóng dung lượng. **Không file nào bị sửa** |
| `Verification failed: '<trường>' ...` | Giá trị không đọc lại được như đã ghi | Restore từ manifest vừa tạo (menu 2); chi tiết trong log |
| `This backup cannot be used: ...` | Manifest lỗi/không phải JSON của MusicTagger | Chọn manifest khác; xem lỗi cụ thể được liệt kê |
| Chữ tiếng Việt hiện `?`/ô vuông | Terminal/console không hỗ trợ UTF-8 | Dùng Windows Terminal/PowerShell 7, hoặc `chcp 65001`; app in cảnh báo khi không đổi được encoding |
| `Note: the terminal does not allow switching to UTF-8` | Encoding bị khoá bởi môi trường | Không chặn app; metadata vẫn được lưu đúng, chỉ hiển thị có thể sai |
| Không thấy thanh tiến trình | Output bị pipe/redirect | Bình thường: app in `Done: n/n` thay cho progress bar |

Log chi tiết: `<app data>/MusicTagger/logs/`.

---

## 14. Giới hạn đã biết

1. **Định dạng hỗ trợ giới hạn ở bảng [1.1](#11-định-dạng-hỗ-trợ).** File `.wma`, `.ape`, `.wv`,
   `.mpc`, `.dsf`, `.wave` và container video (`.mp4`/`.m4v`) bị bỏ qua khi quét — chỉ những định
   dạng đã có test ghi thật mới được đưa vào danh sách sửa.
2. **Chỉ tag text.** Không sửa/xoá ảnh bìa, lời bài hát, replay gain — các frame đó được giữ
   nguyên nhưng không hiển thị trong UI.
3. **Không đổi tên file, không di chuyển/di dời thư mục**, không xoá file.
4. **Không tra cứu metadata online**, không dùng audio fingerprint (và không truy cập mạng).
5. **ID3v1 bị gộp và xoá** khi ghi (để tránh hai tag mâu thuẫn). Nếu thiết bị cực cũ chỉ đọc
   ID3v1 thì nó sẽ không thấy tag sau khi app ghi.
6. **ID3v2.2 chỉ đọc**, khi ghi sẽ nâng lên v2.3/v2.4.
7. **Tương thích tiếng Việt phụ thuộc thiết bị phát** — xem mục 7.
8. **Track/Disc/Year chỉ một cặp giá trị** (`n` hoặc `n/tổng`), không hỗ trợ các định dạng đặc biệt
   như `A1`, `1-3`.
9. **Không có undo nhiều bước**: "undo" = restore manifest tương ứng (mỗi lần ghi tạo một manifest).
10. **Không lưu trạng thái phiên**: thoát app là mất danh sách file đã quét (file trên đĩa không bị
    ảnh hưởng).
11. **Quét thư mục rất lớn** sẽ đọc metadata từng file để hiển thị danh sách — có progress và xử
    lý tuần tự (không nạp toàn bộ audio vào RAM), nhưng chậm hơn so với chỉ liệt kê tên file.
12. **UI tiếng Anh** (SRS cho phép chọn một ngôn ngữ nhất quán cho MVP); tài liệu là tiếng Việt.
13. **Console color** chỉ đổi sau khi khởi động lại app.
14. **WAV luôn lưu ID3v2.4**, không ghi được v2.3 (giới hạn của TagLib#: chunk `id3 ` do thư viện
    tạo luôn là v2.4). Setting ID3 write version vì vậy không áp dụng cho WAV; các định dạng còn
    lại thì áp dụng đúng như bảng ở [mục 7](#7-tag-system-và-encoding).
15. **Tag RIFF INFO/IDVX trên WAV/AIFF bị gộp vào ID3v2 khi ghi** (file chỉ còn một tag). Tool chỉ
    đọc INFO chunk sẽ không thấy metadata sau khi app ghi; giá trị trước khi sửa vẫn nằm trong
    manifest backup để restore.
16. **Định dạng được nhận theo extension**, không dò nội dung file: một file `.mp3` thực chất là
    FLAC sẽ được xử lý như MP3 và có thể bị báo lỗi khi ghi (không làm hỏng file gốc).

---

## 15. Thư viện và giấy phép

| Thư viện | Phiên bản | Giấy phép | Vai trò |
|---|---|---|---|
| [TagLib#](https://github.com/mono/taglib-sharp) (`TagLibSharp`) | 2.3.0 | **LGPL-2.1-only** | Đọc/ghi tag cho mọi container (ID3, Vorbis comment, MP4 atom, RIFF INFO) — toàn bộ nằm trong `MusicTagger.Infrastructure` |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console) | 0.57.2 | MIT | Bảng, panel, progress, màu — chỉ trong `MusicTagger.Cli` |
| [xunit](https://xunit.net/) | 2.9.3 | Apache-2.0 | Test (không phát hành kèm app) |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test (không phát hành kèm app) |

Quyết định chọn thư viện: `TagLibSharp 2.3.0` là bản ổn định, target `netstandard2.0` (dùng được
với .NET 10), giấy phép LGPL-2.1 (dùng dạng thư viện động, có thể thay thế DLL). Phương án `ATL`
trên NuGet khi triển khai chỉ có bản prerelease của CI nên bị loại. `Spectre.Console 0.57.2` (MIT)
target `net10.0`, có sẵn bảng/progress và fallback không màu.

Vì `TagLib#` là LGPL-2.1: nếu bạn phân phối lại app, hãy giữ `TagLibSharp.dll` dưới dạng thư viện
động có thể thay thế và kèm thông báo giấy phép của nó.

---

## 16. Ghi chú về project demo còn sót trong repo

Repo ban đầu có một project scaffold EF Core (`src/MusicTaggerCli/`, demo `Product`) không liên
quan đến SRS. Theo quyết định của chủ repo:

- **Giữ nguyên file trên đĩa**, không sửa nội dung.
- **Không đưa vào `MusicTaggerCli.sln`**: solution chỉ chứa `MusicTagger.Core`,
  `MusicTagger.Infrastructure`, `MusicTagger.Cli` và 3 project test.
- Vì vậy `dotnet build MusicTaggerCli.sln` / `dotnet test MusicTaggerCli.sln` không build project
  demo. Nếu muốn, có thể xoá thư mục đó mà không ảnh hưởng gì đến ứng dụng.

---

## 17. Cấu trúc repo

```text
MusicTaggerCli/
├── docs/MusicTagger_SRS.md          # yêu cầu gốc
├── src/
│   ├── MusicTagger.Cli/             # app console (chạy: dotnet run --project src/MusicTagger.Cli)
│   ├── MusicTagger.Core/
│   ├── MusicTagger.Infrastructure/
│   └── MusicTaggerCli/              # scaffold demo cũ, KHÔNG thuộc solution (xem mục 16)
├── tests/
│   ├── MusicTagger.Core.Tests/
│   ├── MusicTagger.IntegrationTests/
│   └── MusicTagger.Cli.Tests/
├── samples/
│   ├── settings.json                # ví dụ settings
│   └── backup-manifest.example.json # ví dụ manifest backup
├── publish.ps1                      # publish ra 1 file .exe (xem mục 3.1)
├── MusicTaggerCli.sln
└── README.md
```

Thư mục `publish/` (kết quả publish) và `bin/`, `obj/` nằm trong `.gitignore`, không commit.
