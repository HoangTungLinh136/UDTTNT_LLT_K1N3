$replacements = @(
    @("BA\?O CA\?O T\?NG H\?P NHA\?N S\?", "BÁO CÁO TỔNG HỢP NHÂN SỰ"),
    @("BA\?o CA\?o", "Báo Cáo"),
    @("BA\?o cA\?o", "Báo cáo"),
    @("bA\?o cA\?o", "báo cáo"),
    @("BA\?O CA\?O", "BÁO CÁO"),
    @("T\?NG H\?P", "TỔNG HỢP"),
    @("t\?ng h\?p", "tổng hợp"),
    @("NHA\?N S\?", "NHÂN SỰ"),
    @("NhA\?n s\?", "Nhân sự"),
    @("NgA\?y xu\?t", "Ngày xuất"),
    @("B\?ng s\? li\?u", "Bảng số liệu"),
    @("File dA\?nh kA\?m", "File đính kèm"),
    @("dA\?nh kA\?m", "đính kèm"),
    @("NhA\?n viA\?n", "Nhân viên"),
    @("Thu\?ng L\?", "Thưởng Lễ"),
    @("ChuyA\?n c\?n", "Chuyên cần"),
    @("Khen thu\?ng", "Khen thưởng"),
    @("K\? lu\?t", "Kỷ luật"),
    @("Ngh\? phA\?p", "Nghỉ phép"),
    @("Nhi\?m v\?", "Nhiệm vụ"),
    @("HA\?nh d\?ng", "Hành động"),
    @("Chua cA\? d\? li\?u\.", "Chưa có dữ liệu."),
    @("s\? t\? d\?ng hi\?n th\?", "sẽ tự động hiển thị"),
    @("duy\?t h\?p d\?ng", "duyệt hợp đồng"),
    @("Upload BA\?o CA\?o M\?i", "Upload Báo Cáo Mới"),
    @("TiA\?u d\?", "Tiêu đề"),
    @("d\?\?\?", "📊"),
    @("DA\?nh cho Ban LA\?nh \?o", "Dành cho Ban Lãnh đạo"),
    @("Ban LA\?nh \?o", "Ban Lãnh đạo"),
    @("T\? ch\?i", "Từ chối"),
    @("Ch\? duy\?t", "Chờ duyệt"),
    @("DA\? duy\?t", "Đã duyệt"),
    @("PhA\?n lo\?i", "Phân loại"),
    @("TA\?n File", "Tên File"),
    @("Ngu\?i t\?i lA\?n", "Người tải lên"),
    @("Th\?i gian", "Thời gian"),
    @("T\?i xu\?ng", "Tải xuống"),
    @("Chua cA\?", "Chưa có"),
    @("nA\?o du\?c t\?i lA\?n", "nào được tải lên"),
    @("t\?i lA\?n", "tải lên"),
    @("luong thA\?ng 12", "lương tháng 12"),
    @("KhA\?c", "Khác"),
    @("Báo cáo nhA\?n s\?", "Báo cáo nhân sự"),
    @("Báo cáo ch\?m cA\?ng", "Báo cáo chấm công"),
    @("Báo cáo luong", "Báo cáo lương"),
    @("Ch\?n lo\?i", "Chọn loại"),
    @("Ch\?n file", "Chọn file"),
    @("H\?y", "Hủy"),
    @("G\?i", "Gửi"),
    @("ThA\?nh cA\?ng", "Thành công"),
    @("L\?i!", "Lỗi!"),
    @("CA\? File", "Có File"),
    @("t\? d\?ng thA\?m dA\?ng", "tự động thêm dòng"),
    @("m\?t file", "một file"),
    @("v\?a ghi nh\?n", "vừa ghi nhận"),
    @("T\?i l\?i trang d\? xem", "Tải lại trang để xem"),
    @("dA\? k\?t n\?i", "đã kết nối"),

    # Layout & others (A / A / Â / ?)
    @("NhAn viAn", "Nhân viên"),
    @("Từ ngAy", "Từ ngày"),
    @("Đến ngAy", "Đến ngày"),
    @("LA do", "Lý do"),
    @("Trạng thAi", "Trạng thái"),
    @("HAnh động", "Hành động"),
    @("MA NV", "Mã NV"),
    @("Chưa có dữ liệu nghỉ phAp.", "Chưa có dữ liệu nghỉ phép."),
    @("nghỉ phAp nAy", "nghỉ phép này"),
    @("XAc nhận", "Xác nhận"),
    @("ThAnh cAng!", "Thành công!"),
    @("Đơn đã được xử lA.", "Đơn đã được xử lý."),
    @("KhAng thể thực hiện thao tAc.", "Không thể thực hiện thao tác."),
    @("thao tAc", "thao tác"),
    @("Cảnh bAo", "Cảnh báo"),
    @("Đồng A", "Đồng ý"),
    @("ĐA xAa!", "Đã xóa!"),
    @("AM THANH THANG BAO", "ÂM THANH THÔNG BÁO"),
    @("KhAng", "Không"),
    @("mAnh", "mình"),
    @("nhAn sự", "nhân sự"),
    @("tAi", "tôi"),
    @("ThAnh cAng", "Thành công"),
    @("nghỉ phAp", "nghỉ phép"),
    @("nAy", "này"),
    @("XAa", "Xóa"),
    @("xAa", "xóa"),
    @("ĐA", "Đã"),
    @("thAnh cAng", "thành công"),
    @("cảnh bAo", "cảnh báo"),
    @("xử lA", "xử lý"),
    @("KhAng thể", "Không thể"),

    # Encoding Artifacts
    @("Quáº£n lĂ½ Nghá»‰ phĂ©p", "Quản lý Nghỉ phép"),
    @("Danh sĂ¡ch Ä‘Æ¡n nghá»‰ phĂ©p \(Quáº£n lĂ½\)", "Danh sách đơn nghỉ phép (Quản lý)"),
    @("Ä Æ¡n nghá»‰ phĂ©p cá»§a tĂ´i", "Đơn nghỉ phép của tôi"),
    @("Táº¡o Ä‘Æ¡n nghá»‰ má»›i", "Tạo đơn nghỉ mới"),
    @("Cháº¿ Ä‘á»™: Quáº£n trá»‹ viĂªn", "Chế độ: Quản trị viên"),
    @("â ³ Chá»  duyá»‡t", "⏳ Chờ duyệt"),
    @("âœ… Ä Ă£ duyá»‡t", "✅ Đã duyệt"),
    @("â Œ Tá»« chá»‘i", "❌ Từ chối"),
    @("Chá»  duyá»‡t", "Chờ duyệt"),
    @("Ä Ă£ duyá»‡t", "Đã duyệt"),
    @("Tá»« chá»‘i", "Từ chối"),
    @("Xem Ä‘Æ¡n", "Xem đơn"),
    @("Duyá»‡t", "Duyệt"),
    @("Báº¡n cĂ³ cháº¯c cháº¯n muá»‘n", "Bạn có chắc chắn muốn"),
    @("Há»§y Ä‘Æ¡n", "Hủy đơn"),
    @("Lá»—i!", "Lỗi!"),
    @("CĂ³ lá»—i xáº£y ra", "Có lỗi xảy ra"),
    @("Dá»¯ liá»‡u Ä‘Ă£ Ä‘Æ°á»£c cáº­p nháº­t", "Dữ liệu đã được cập nhật"),
    @("Quáº£n lĂ½", "Quản lý"),
    @("Nghá»‰ phĂ©p", "Nghỉ phép"),
    @("Cháº¥m cÃ´ng", "Chấm công"),
    @("NhÃ¢n viÃªn", "Nhân viên")
)

$files = Get-ChildItem -Path "Views" -Recurse -Filter "*.cshtml"

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    
    $changed = $false
    foreach ($pair in $replacements) {
        $pattern = $pair[0]
        $replacement = $pair[1]
        
        $fullPattern = "(?-i)$pattern"
        if ($content -match $fullPattern) {
            $content = $content -replace $fullPattern, $replacement
            $changed = $true
        }
    }
    
    if ($changed) {
        $utf8WithBom = New-Object System.Text.UTF8Encoding $true
        [System.IO.File]::WriteAllText($file.FullName, $content, $utf8WithBom)
        Write-Host "Fixed: $($file.Name)"
    }
}
