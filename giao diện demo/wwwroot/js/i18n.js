/**
 * HRMS i18n - Hệ thống đa ngôn ngữ (Tiếng Việt / English)
 * Lưu trữ: localStorage key "hrms_lang"
 * Sử dụng: data-i18n="key" trên các HTML element
 * Fallback: Giữ nguyên text Tiếng Việt nếu thiếu key
 * Version: 2.0 - Full 100% coverage
 */

window.HRMS_i18n = (function () {

    // ============================================================
    // TỪ ĐIỂN NGÔN NGỮ - TIẾNG VIỆT
    // ============================================================
    const translations = {
        vi: {
            // === SIDEBAR - Navigation ===
            "nav.dashboard":            "🏠 Bảng điều khiển",
            "nav.home":                 "🏠 Trang chủ",
            "nav.my_profile":           "👤 Hồ sơ của tôi",
            "nav.personal_profile":     "👤 Hồ sơ cá nhân",
            "nav.manage_accounts":      "👥 Quản lý tài khoản",
            "nav.permissions":          "🛡 Phân quyền",
            "nav.salary_payment":       "💳 Thanh toán lương",
            "nav.attendance":           "⏱ Chấm công",
            "nav.leave":                "📄 Nghỉ phép",
            "nav.contract":             "📑 Hợp đồng",
            "nav.manage_employees":     "👥 Quản lý nhân viên",
            "nav.salary":               "💰 Lương",
            "nav.report":               "📊 Báo cáo",
            "nav.export_report":        "📥 Xuất báo cáo",
            "nav.notifications":        "🔔 Thông báo",
            "nav.logout":               "🚪 Đăng xuất",
            "nav.adjustment":           "🔧 Điều chỉnh chấm công",

            // === TOPBAR ===
            "topbar.lang_btn":          "🇻🇳 Tiếng Việt",
            "topbar.lang_vi":           "🇻🇳 Tiếng Việt",
            "topbar.lang_en":           "🇬🇧 English",
            "topbar.sound_on":          "🔊",
            "topbar.sound_off":         "🔇",
            "topbar.sound_tooltip_on":  "Tắt âm thanh thông báo",
            "topbar.sound_tooltip_off": "Bật âm thanh thông báo",

            // === DASHBOARD ===
            "dashboard.title":          "📊 Trang chủ hệ thống",
            "dashboard.welcome":        "Xin chào",
            "dashboard.total_emp":      "👨‍💼 Tổng nhân viên",
            "dashboard.active":         "✅ Đang làm",
            "dashboard.today_checkin":  "Đã chấm công hôm nay",
            "dashboard.pending_request":"Yêu cầu chờ duyệt",

            // === EMPLOYEE ===
            "employee.title":           "👥 Danh sách hồ sơ nhân sự hệ thống",
            "employee.my_profile":      "Hồ sơ của tôi",
            "employee.name":            "Họ và Tên",
            "employee.code":            "Mã NV",
            "employee.department":      "Phòng ban",
            "employee.position":        "Chức vụ",
            "employee.job_title":       "Chức danh",
            "employee.email":           "Email",
            "employee.phone":           "Số điện thoại",
            "employee.address":         "Địa chỉ",
            "employee.dob":             "Ngày sinh",
            "employee.start_date":      "Ngày vào làm",
            "employee.status":          "Trạng thái",
            "employee.add":             "➕ Thêm nhân viên",
            "employee.edit":            "Sửa",
            "employee.delete":          "Xóa",
            "employee.details":         "Chi tiết",
            "employee.view_profile":    "Xem hồ sơ",
            "employee.save":            "Lưu",
            "employee.cancel":          "Hủy",
            "employee.search":          "Tìm tên hoặc mã nhân viên (VD: NV001)...",
            "employee.filter_dept":     "-- Tất cả phòng ban --",
            "employee.filter_btn":      "Lọc dữ liệu",
            "employee.reset_btn":       "Đặt lại",
            "employee.academic":        "Trình độ",
            "employee.salary_col":      "Mức lương",
            "employee.actions":         "Thao tác",
            "employee.no_data":         "Không tìm thấy dữ liệu nhân sự",
            "employee.no_data_hint":    "Vui lòng kiểm tra lại từ khóa hoặc mã nhân viên.",
            "employee.personal_info":   "📌 Thông tin cá nhân",
            "employee.work_info":       "💼 Công việc & Tài chính",
            "employee.phone_label":     "Số điện thoại",
            "employee.email_label":     "Email liên hệ",
            "employee.gender_dob":      "Giới tính / Ngày sinh",
            "employee.citizen_id":      "Số CCCD",
            "employee.permanent_addr":  "Địa chỉ thường trú",
            "employee.education":       "🎓 Trình độ học vấn",
            "employee.qual_allowance":  "Phụ cấp bằng cấp",
            "employee.base_salary":     "Mức lương cơ bản",
            "employee.bank_account":    "Tài khoản ngân hàng",
            "employee.not_updated":     "Chưa cập nhật",
            "employee.no_email":        "Chưa có email",
            "employee.no_bank":         "Chưa cập nhật",
            "employee.back_list":       "Quay lại danh sách",
            "employee.edit_profile":    "Sửa hồ sơ",
            "employee.emp_id_label":    "Mã nhân viên:",
            "employee.add_new":         "Thêm nhân viên mới",
            "employee.create_account":  "Tạo tài khoản đăng nhập",
            "employee.gender":          "Giới tính",
            "employee.per_month":       "/tháng",
            "employee.per_hour":        "VNĐ/giờ",
            "employee.no_allowance":    "Không có",
            "employee.new_badge":       "NV-MỚI",
            "employee.no_job_title":    "Chưa cập nhật",
            "employee.update_profile":  "CẬP NHẬT HỒ SƠ CÁ NHÂN",
            "employee.change_photo":    "Thay đổi ảnh",
            "employee.save_changes":    "Lưu thay đổi",
            "employee.edit_section":    "CẬP NHẬT HỒ SƠ",
            "employee.create_title":    "THÊM NHÂN VIÊN MỚI",

            // === ATTENDANCE ===
            "attendance.title":         "⏱ Chấm công",
            "attendance.checkin":       "⏱ Giờ Check-in",
            "attendance.checkout":      "⏳ Giờ Check-out",
            "attendance.date":          "Ngày",
            "attendance.std_hours":     "Giờ tiêu chuẩn",
            "attendance.ot_hours":      "Giờ tăng ca",
            "attendance.status":        "Trạng thái",
            "attendance.note":          "Ghi chú",
            "attendance.history":       "📋 Tổng công tháng",
            "attendance.statistics":    "Thống kê",
            "attendance.checkin_now":   "Chấm công vào",
            "attendance.checkout_now":  "Chấm công ra",
            "attendance.adjustment":    "Yêu cầu điều chỉnh",
            "attendance.month":         "Tháng",
            "attendance.year":          "Năm",
            "attendance.checkin_time":  "Thời gian Check In",
            "attendance.checkout_time": "Thời gian Check Out",
            "attendance.total_hours":   "Tổng giờ làm",
            "attendance.completed":     "Đã hoàn thành",
            "attendance.not_checkout":  "Chưa Check-out",
            "attendance.stt":           "STT",
            "attendance.filter_month":  "Lọc theo tháng",
            "attendance.filter_year":   "Lọc theo năm",
            "attendance.my_history":    "Lịch sử chấm công của tôi",
            "attendance.all_history":   "Lịch sử chấm công toàn bộ",
            "attendance.employee_col":  "Nhân viên",
            "attendance.dept_col":      "Phòng ban",
            "attendance.no_record":     "Chưa có dữ liệu chấm công.",
            "attendance.adj_request":   "Yêu cầu điều chỉnh",
            "attendance.late":          "Đi trễ",
            "attendance.early":         "Ra sớm",
            "attendance.normal":        "Bình thường",

            // === ATTENDANCE ADJUSTMENT ===
            "adj.title":                "⚙️ Quản lý Yêu cầu Điều chỉnh Chấm công",
            "adj.req_checkin":          "Check-in đề xuất",
            "adj.req_checkout":         "Check-out đề xuất",
            "adj.reason":               "Lý do",
            "adj.status":               "Trạng thái",
            "adj.pending":              "🟡 Chờ xử lý",
            "adj.approved":             "🟢 Đã duyệt",
            "adj.rejected":             "🔴 Từ chối",
            "adj.submit":               "Gửi yêu cầu",
            "adj.manage":               "Quản lý yêu cầu",
            "adj.approve":              "Duyệt",
            "adj.reject":               "Từ chối",
            "adj.reject_reason":        "Lý do từ chối",
            "adj.attachment":           "Tài liệu đính kèm",
            "adj.view_detail":          "Xem chi tiết",
            "adj.created_at":           "Ngày tạo",
            "adj.attend_record":        "Bản ghi CC",
            "adj.no_requests":          "Chưa có yêu cầu nào.",
            "adj.my_requests":          "Yêu cầu điều chỉnh của tôi",
            "adj.confirm_approve":      "Duyệt yêu cầu này?",
            "adj.confirm_reject":       "Từ chối yêu cầu này?",

            // === LEAVE ===
            "leave.title":              "📄 Nghỉ phép",
            "leave.management":         "Danh sách đơn nghỉ phép (Quản lý)",
            "leave.my_leave":           "Đơn nghỉ phép của tôi",
            "leave.type":               "Loại nghỉ phép",
            "leave.from":               "Từ ngày",
            "leave.to":                 "Đến ngày",
            "leave.reason":             "Lý do",
            "leave.status":             "Trạng thái",
            "leave.apply":              "➕ Tạo đơn nghỉ mới",
            "leave.approve":            "Duyệt",
            "leave.reject":             "Từ chối",
            "leave.pending":            "⏳ Chờ duyệt",
            "leave.approved":           "✅ Đã duyệt",
            "leave.rejected":           "❌ Từ chối",
            "leave.days":               "Số ngày",
            "leave.view":               "Xem đơn",
            "leave.delete":             "Xóa",
            "leave.cancel":             "Hủy đơn",
            "leave.no_data":            "Chưa có dữ liệu nghỉ phép.",
            "leave.mode_admin":         "Chế độ: Quản trị viên (HR)",
            "leave.confirm_approve":    "DUYỆT",
            "leave.confirm_reject":     "TỪ CHỐI",
            "leave.confirm_title":      "Xác nhận",
            "leave.confirm_msg":        "Bạn có chắc chắn muốn {action} đơn nghỉ phép này?",
            "leave.success_title":      "Thành công!",
            "leave.success_msg":        "Đơn đã được xử lý.",
            "leave.delete_confirm":     "Cảnh báo",
            "leave.delete_hr_msg":      "Bạn chắc chắn muốn xóa đơn này khỏi hệ thống?",
            "leave.delete_emp_msg":     "Bạn muốn hủy đơn xin nghỉ này?",
            "leave.ok":                 "Đồng ý",
            "leave.back":               "Quay lại",
            "leave.deleted":            "Đã xóa!",
            "leave.updated":            "Dữ liệu đã được cập nhật.",
            "leave.err":                "Lỗi!",
            "leave.err_msg":            "Không thể thực hiện thao tác.",
            "leave.err_delete":         "Có lỗi xảy ra khi xóa.",

            // === CONTRACT ===
            "contract.title":           "📑 Hợp đồng lao động",
            "contract.management":      "Quản lý Hợp đồng Nhân viên",
            "contract.my_contracts":    "Hợp đồng của tôi",
            "contract.type":            "Loại hợp đồng",
            "contract.start":           "Ngày bắt đầu",
            "contract.end":             "Ngày kết thúc",
            "contract.salary":          "Lương cơ bản",
            "contract.status":          "Trạng thái",
            "contract.sign":            "✍️ Ký hợp đồng",
            "contract.view":            "Xem chi tiết",
            "contract.view_again":      "Xem lại",
            "contract.download":        "Tải xuống",
            "contract.create":          "Soạn hợp đồng mới",
            "contract.cancel":          "Hủy bỏ",
            "contract.waiting_sign":    "📝 Chờ ký",
            "contract.waiting_approve": "⏳ Chờ duyệt",
            "contract.no_data":         "Hiện tại không có hợp đồng nào trong danh sách.",
            "contract.emp_name":        "Họ tên Nhân viên",
            "contract.modal_title":     "📝 Soạn thảo Hợp đồng Lao động",
            "contract.approve":         "✅ Phê duyệt (Hoàn tất)",
            "contract.sign_title":      "✍️ Ký Hợp đồng",
            "contract.sign_here":       "Nhập tên đầy đủ để ký...",
            "contract.confirm_sign":    "Xác nhận ký",
            "contract.emp_signature":   "Chữ ký Nhân viên:",
            "contract.hr_signature":    "Chữ ký HR:",
            "contract.signed":          "✅ Đã ký",
            "contract.not_signed":      "Chưa ký",

            // === SALARY ===
            "salary.title":             "💰 Bảng lương",
            "salary.my_salary":         "Lịch sử lương của tôi",
            "salary.company_salary":    "Bảng lương toàn công ty",
            "salary.period":            "Kỳ lương",
            "salary.base":              "Lương CB",
            "salary.allowance":         "Phụ cấp",
            "salary.deduction":         "Giảm trừ & Thuế",
            "salary.net":               "Thực nhận (Net)",
            "salary.gross":             "Thu nhập (Gross)",
            "salary.work_days":         "Giờ làm",
            "salary.ot_hours":          "Giờ tăng ca",
            "salary.calculate":         "Tính lương",
            "salary.export":            "Xuất file CSV",
            "salary.print":             "Xuất PDF / In toàn bộ",
            "salary.insurance":         "Bảo hiểm (10.5%)",
            "salary.tax":               "Thuế TNCN (5 bậc)",
            "salary.other_deduction":   "Khác",
            "salary.total_income":      "Tổng thu nhập",
            "salary.print_slip":        "In phiếu lương",
            "salary.no_data":           "Chưa có dữ liệu lương.",

            // === PAYROLL PAYMENT ===
            "payment.title":            "💳 Thanh toán lương",
            "payment.confirm":          "Tạo đợt thanh toán",
            "payment.status_paid":      "Đã thanh toán",
            "payment.status_pending":   "Số nhân viên đang chọn",
            "payment.batch_pay":        "Thanh toán hàng loạt",
            "payment.pay_all":          "Thanh toán tất cả",
            "payment.amount":           "Tổng tiền thanh toán (đã chọn)",
            "payment.date":             "Ngày thanh toán",
            "payment.method":           "Phương thức",
            "payment.mark_paid":        "Xác nhận đã thanh toán",
            "payment.period":           "Kỳ lương",
            "payment.dept":             "Phòng ban",
            "payment.pay_status":       "Trạng thái TT",
            "payment.bank":             "Tài khoản ngân hàng",
            "payment.net_salary":       "Lương thực nhận",
            "payment.all_status":       "-- Tất cả TT --",
            "payment.paid_badge":       "✅ Đã TT",
            "payment.unpaid_badge":     "⏳ Chờ TT",
            "payment.processing_badge": "🔄 Đang xử lý",
            "payment.no_data":          "Không có dữ liệu.",
            "payment.select_all":       "Chọn tất cả",
            "payment.no_bank_warn":     "Chưa có tài khoản ngân hàng",
            "payment.filter_btn":       "Lọc",

            // === NOTIFICATION ===
            "notif.title":              "Quản lý Thông báo",
            "notif.system_title":       "📢 Thông báo hệ thống",
            "notif.send_to":            "👤 Gửi tới đối tượng",
            "notif.content":            "📝 Nội dung thông báo",
            "notif.send_btn":           "🚀 Gửi đi",
            "notif.delete":             "🗑 Xóa",
            "notif.recall":             "🔄 Thu hồi",
            "notif.send_all":           "📢 Gửi cho tất cả nhân viên",
            "notif.send_hr":            "📩 Gửi cho Nhân sự (HR)",
            "notif.send_admin":         "⚙️ Gửi cho Admin hệ thống",
            "notif.send_leader":        "🏗️ Gửi cho Ban lãnh đạo",
            "notif.personal":           "Gửi đích danh nhân viên",
            "notif.empty":              "Chưa có thông báo nào dành cho bạn.",
            "notif.from":               "Từ:",
            "notif.to":                 "Đến:",
            "notif.placeholder":        "Nhập tin nhắn cần phát đi...",
            "notif.online":             "● Trực tuyến",
            "notif.connecting":         "Đang kết nối Real-time...",
            "notif.recall_confirm":     "Thu hồi?",
            "notif.recall_text":        "Tin nhắn sẽ biến mất ở mọi máy khách!",
            "notif.recall_ok":          "Đồng ý",
            "notif.sent_success":       "Đã phát đi!",
            "notif.send_general":       "Gửi chung",
            "notif.send_management":    "Gửi Ban quản trị",

            // === REPORT ===
            "report.title":             "📊 Báo cáo tổng hợp Nhân sự",
            "report.export":            "Xuất báo cáo",
            "report.period":            "Kỳ báo cáo",
            "report.type":              "Loại báo cáo",
            "report.generate":          "Tạo báo cáo",
            "report.table_tab":         "Bảng số liệu",
            "report.file_tab":          "File đính kèm",
            "report.print":             "In Báo Cáo",

            // === COMMON ===
            "common.save":              "Lưu",
            "common.cancel":            "Hủy",
            "common.edit":              "Chỉnh sửa",
            "common.delete":            "Xóa",
            "common.confirm":           "Xác nhận",
            "common.close":             "Đóng",
            "common.search":            "Tìm kiếm",
            "common.filter":            "Lọc dữ liệu",
            "common.export":            "Xuất",
            "common.print":             "In",
            "common.back":              "Quay lại",
            "common.next":              "Tiếp theo",
            "common.loading":           "Đang tải...",
            "common.no_data":           "Không có dữ liệu",
            "common.success":           "Thành công",
            "common.error":             "Có lỗi xảy ra",
            "common.warning":           "Cảnh báo",
            "common.yes":               "Có",
            "common.no":                "Không",
            "common.from":              "Từ",
            "common.to":                "Đến",
            "common.actions":           "Thao tác",
            "common.status":            "Trạng thái",
            "common.detail":            "Chi tiết",
            "common.total":             "Tổng cộng",
            "common.stt":               "STT",
            "common.add":               "Thêm",
            "common.view":              "Xem",
            "common.reset":             "Đặt lại",
            "common.approve":           "Duyệt",
            "common.reject":            "Từ chối",
            "common.time":              "Thời gian",
            "common.category":          "Phân loại",
            "common.event_msg":         "Nội dung sự kiện / Tin nhắn",
            "common.need_process":      "CẦN XỬ LÝ",
            "common.system":            "Hệ thống",
            "common.system_online":     "● Hệ thống quản trị HRMS trực tuyến",
            "common.connecting":        "Đang kết nối...",
            "common.online":            "● Đang trực tuyến",
            "common.from_label":        "TỪ:",
            "common.msg_incoming":      "📩 Tin nhắn đến",
            "common.checkin_log":       "⏱ Chấm công",
            "common.leave_log":         "📄 Nghỉ phép",
            "common.contract_log":      "📑 Hợp đồng",
            "common.approve_log":       "✅ Phê duyệt",
            "common.log_title":         "🔔 Nhật ký hoạt động & Tin nhắn từ các Actor",
            "common.select_all":        "-- Tất cả --",
            "common.view_detail":       "Xem chi tiết",

            // === STATUS ===
            "status.active":            "✅ Đang làm",
            "status.inactive":          "❌ Nghỉ việc",
            "status.pending":           "Chờ duyệt",
            "status.approved":          "Đã duyệt",
            "status.rejected":          "Từ chối",
            "status.paid":              "Đã thanh toán",
            "status.unpaid":            "Chưa thanh toán",
            "status.official":          "Chính thức",
            "status.probation":         "Thử việc",
            "status.completed":         "Đã hoàn thành",
            "status.not_checkout":      "Chưa Check-out",
            "status.processing":        "Đang xử lý",
            "status.working":           "Đang làm việc",

            // === ACCOUNT / LOGIN ===
            "account.login_title":      "Đăng nhập - HRMS",
            "account.username":         "Tài khoản",
            "account.password":         "Mật khẩu",
            "account.remember":         "Lưu thông tin đăng nhập",
            "account.login_btn":        "Đăng nhập",
            "account.forgot":           "Bạn quên mật khẩu?",
            "account.logout":           "Đăng xuất",
            "account.err_no_user":      "Vui lòng nhập tài khoản",
            "account.err_no_pass":      "Vui lòng nhập mật khẩu",
            "account.err_invalid":      "Tên đăng nhập hoặc mật khẩu không đúng",

            // === VALIDATION / ERROR MESSAGES ===
            "msg.delete_confirm":       "Bạn có chắc chắn muốn xóa?",
            "msg.delete_permanent":     "Dữ liệu hồ sơ sẽ bị xóa vĩnh viễn!",
            "msg.delete_ok":            "Đồng ý xóa",
            "msg.invalid_data":         "Dữ liệu không hợp lệ",
            "msg.not_found":            "Không tìm thấy dữ liệu",
            "msg.save_success":         "Lưu thành công",
            "msg.save_error":           "Có lỗi xảy ra",
            "msg.system_added":         "đã được thêm vào danh sách!",
            "msg.list_empty":           "Danh sách hiện đang trống.",
            "msg.contract_approved":    "đã được duyệt hợp đồng và trở thành nhân sự chính thức.",
            "msg.welcome_new":          "🎉 Chúc mừng! đã chính thức gia nhập đội ngũ.",
            "msg.checkin_done":         "vừa check-in",
            "msg.leave_submitted":      "vừa gửi đơn nghỉ phép",
            "msg.contract_requested":   "yêu cầu ký hợp đồng",
            "msg.new_msg_from":         "📩 Tin nhắn mới từ",

            // === AI ASSISTANT ===
            "ai.title":                 "🤖 Trợ lý HRMS",
            "ai.reset":                 "Làm mới",
            "ai.placeholder":           "Hỏi tôi về lương, nhân viên...",
            "ai.send":                  "Gửi",
            "ai.greeting":              "Chào Bạn! Tôi là trợ lý AI. Bạn cần tôi giúp gì về nhân sự hôm nay?",
            "ai.thinking":              "🤖 Đang suy nghĩ...",
            "ai.error":                 "⚠️ Lỗi server rồi Bạn ơi!",
            "ai.reset_msg":             "Đã làm mới lịch sử chat! Tôi có thể giúp gì cho bạn?",

            // === PERMISSION / ADMIN ===
            "perm.title":               "🛡 Quản lý Phân quyền",
            "perm.role":                "Vai trò",
            "perm.assign":              "Gán quyền",
            "perm.hr":                  "HR",
            "perm.admin":               "Admin",
            "perm.employee":            "Nhân viên",
            "perm.leader":              "Trưởng phòng",
        },

        // ============================================================
        // TỪ ĐIỂN NGÔN NGỮ - ENGLISH
        // ============================================================
        en: {
            // === SIDEBAR - Navigation ===
            "nav.dashboard":            "🏠 Dashboard",
            "nav.home":                 "🏠 Home",
            "nav.my_profile":           "👤 My Profile",
            "nav.personal_profile":     "👤 Personal Profile",
            "nav.manage_accounts":      "👥 Account Management",
            "nav.permissions":          "🛡 Permissions",
            "nav.salary_payment":       "💳 Salary Payment",
            "nav.attendance":           "⏱ Attendance",
            "nav.leave":                "📄 Leave",
            "nav.contract":             "📑 Contract",
            "nav.manage_employees":     "👥 Employee Management",
            "nav.salary":               "💰 Payroll",
            "nav.report":               "📊 Reports",
            "nav.export_report":        "📥 Export Report",
            "nav.notifications":        "🔔 Notifications",
            "nav.logout":               "🚪 Logout",
            "nav.adjustment":           "🔧 Attendance Adjustment",

            // === TOPBAR ===
            "topbar.lang_btn":          "🇬🇧 English",
            "topbar.lang_vi":           "🇻🇳 Tiếng Việt",
            "topbar.lang_en":           "🇬🇧 English",
            "topbar.sound_on":          "🔊",
            "topbar.sound_off":         "🔇",
            "topbar.sound_tooltip_on":  "Mute notification sound",
            "topbar.sound_tooltip_off": "Enable notification sound",

            // === DASHBOARD ===
            "dashboard.title":          "📊 System Dashboard",
            "dashboard.welcome":        "Welcome",
            "dashboard.total_emp":      "👨‍💼 Total Employees",
            "dashboard.active":         "✅ Active",
            "dashboard.today_checkin":  "Checked In Today",
            "dashboard.pending_request":"Pending Requests",

            // === EMPLOYEE ===
            "employee.title":           "👥 Employee Personnel Records",
            "employee.my_profile":      "My Profile",
            "employee.name":            "Full Name",
            "employee.code":            "Employee ID",
            "employee.department":      "Department",
            "employee.position":        "Position",
            "employee.job_title":       "Job Title",
            "employee.email":           "Email",
            "employee.phone":           "Phone Number",
            "employee.address":         "Address",
            "employee.dob":             "Date of Birth",
            "employee.start_date":      "Start Date",
            "employee.status":          "Status",
            "employee.add":             "➕ Add Employee",
            "employee.edit":            "Edit",
            "employee.delete":          "Delete",
            "employee.details":         "Details",
            "employee.view_profile":    "View Profile",
            "employee.save":            "Save",
            "employee.cancel":          "Cancel",
            "employee.search":          "Search by employee name or ID (e.g. NV001)...",
            "employee.filter_dept":     "-- All Departments --",
            "employee.filter_btn":      "Filter",
            "employee.reset_btn":       "Reset",
            "employee.academic":        "Education",
            "employee.salary_col":      "Salary",
            "employee.actions":         "Actions",
            "employee.no_data":         "No employee records found",
            "employee.no_data_hint":    "Please check your keyword or employee code.",
            "employee.personal_info":   "📌 Personal Information",
            "employee.work_info":       "💼 Work & Finance",
            "employee.phone_label":     "Phone Number",
            "employee.email_label":     "Contact Email",
            "employee.gender_dob":      "Gender / Date of Birth",
            "employee.citizen_id":      "National ID",
            "employee.permanent_addr":  "Permanent Address",
            "employee.education":       "🎓 Education Level",
            "employee.qual_allowance":  "Qualification Allowance",
            "employee.base_salary":     "Base Salary",
            "employee.bank_account":    "Bank Account",
            "employee.not_updated":     "Not provided",
            "employee.no_email":        "No email provided",
            "employee.no_bank":         "Not provided",
            "employee.back_list":       "Back to list",
            "employee.edit_profile":    "Edit Profile",
            "employee.emp_id_label":    "Employee ID:",
            "employee.add_new":         "Add New Employee",
            "employee.create_account":  "Create login account",
            "employee.gender":          "Gender",
            "employee.per_month":       "/month",
            "employee.per_hour":        "VND/hr",
            "employee.no_allowance":    "None",
            "employee.new_badge":       "NEW-EMP",
            "employee.no_job_title":    "Not set",
            "employee.update_profile":  "UPDATE PERSONAL PROFILE",
            "employee.change_photo":    "Change Photo",
            "employee.save_changes":    "Save Changes",
            "employee.edit_section":    "UPDATE PROFILE",
            "employee.create_title":    "ADD NEW EMPLOYEE",

            // === ATTENDANCE ===
            "attendance.title":         "⏱ Attendance",
            "attendance.checkin":       "⏱ Check-in Time",
            "attendance.checkout":      "⏳ Check-out Time",
            "attendance.date":          "Date",
            "attendance.std_hours":     "Standard Hours",
            "attendance.ot_hours":      "Overtime Hours",
            "attendance.status":        "Status",
            "attendance.note":          "Note",
            "attendance.history":       "📋 Monthly Work Days",
            "attendance.statistics":    "Statistics",
            "attendance.checkin_now":   "Clock In",
            "attendance.checkout_now":  "Clock Out",
            "attendance.adjustment":    "Adjustment Request",
            "attendance.month":         "Month",
            "attendance.year":          "Year",
            "attendance.checkin_time":  "Check-in Time",
            "attendance.checkout_time": "Check-out Time",
            "attendance.total_hours":   "Total Hours",
            "attendance.completed":     "Completed",
            "attendance.not_checkout":  "Not Checked Out",
            "attendance.stt":           "No.",
            "attendance.filter_month":  "Filter by month",
            "attendance.filter_year":   "Filter by year",
            "attendance.my_history":    "My Attendance History",
            "attendance.all_history":   "All Attendance Records",
            "attendance.employee_col":  "Employee",
            "attendance.dept_col":      "Department",
            "attendance.no_record":     "No attendance records found.",
            "attendance.adj_request":   "Adjustment Request",
            "attendance.late":          "Late",
            "attendance.early":         "Early leave",
            "attendance.normal":        "Normal",

            // === ATTENDANCE ADJUSTMENT ===
            "adj.title":                "⚙️ Manage Attendance Adjustment Requests",
            "adj.req_checkin":          "Requested Check-in",
            "adj.req_checkout":         "Requested Check-out",
            "adj.reason":               "Reason",
            "adj.status":               "Status",
            "adj.pending":              "🟡 Pending",
            "adj.approved":             "🟢 Approved",
            "adj.rejected":             "🔴 Rejected",
            "adj.submit":               "Submit Request",
            "adj.manage":               "Manage Requests",
            "adj.approve":              "Approve",
            "adj.reject":               "Reject",
            "adj.reject_reason":        "Rejection Reason",
            "adj.attachment":           "Attachments",
            "adj.view_detail":          "View Details",
            "adj.created_at":           "Created At",
            "adj.attend_record":        "Attendance Record",
            "adj.no_requests":          "No requests found.",
            "adj.my_requests":          "My Adjustment Requests",
            "adj.confirm_approve":      "Approve this request?",
            "adj.confirm_reject":       "Reject this request?",

            // === LEAVE ===
            "leave.title":              "📄 Leave Management",
            "leave.management":         "Leave Requests (Manager)",
            "leave.my_leave":           "My Leave Requests",
            "leave.type":               "Leave Type",
            "leave.from":               "From Date",
            "leave.to":                 "To Date",
            "leave.reason":             "Reason",
            "leave.status":             "Status",
            "leave.apply":              "➕ Create New Request",
            "leave.approve":            "Approve",
            "leave.reject":             "Reject",
            "leave.pending":            "⏳ Pending",
            "leave.approved":           "✅ Approved",
            "leave.rejected":           "❌ Rejected",
            "leave.days":               "Number of Days",
            "leave.view":               "View",
            "leave.delete":             "Delete",
            "leave.cancel":             "Cancel",
            "leave.no_data":            "No leave records found.",
            "leave.mode_admin":         "Mode: Administrator (HR)",
            "leave.confirm_approve":    "APPROVE",
            "leave.confirm_reject":     "REJECT",
            "leave.confirm_title":      "Confirm",
            "leave.confirm_msg":        "Are you sure you want to {action} this leave request?",
            "leave.success_title":      "Success!",
            "leave.success_msg":        "Request has been processed.",
            "leave.delete_confirm":     "Warning",
            "leave.delete_hr_msg":      "Are you sure you want to delete this record?",
            "leave.delete_emp_msg":     "Do you want to cancel this leave request?",
            "leave.ok":                 "Confirm",
            "leave.back":               "Back",
            "leave.deleted":            "Deleted!",
            "leave.updated":            "Data has been updated.",
            "leave.err":                "Error!",
            "leave.err_msg":            "Unable to perform this action.",
            "leave.err_delete":         "An error occurred while deleting.",

            // === CONTRACT ===
            "contract.title":           "📑 Employment Contract",
            "contract.management":      "Employee Contract Management",
            "contract.my_contracts":    "My Contracts",
            "contract.type":            "Contract Type",
            "contract.start":           "Start Date",
            "contract.end":             "End Date",
            "contract.salary":          "Base Salary",
            "contract.status":          "Status",
            "contract.sign":            "✍️ Sign Contract",
            "contract.view":            "View Details",
            "contract.view_again":      "View",
            "contract.download":        "Download",
            "contract.create":          "Create New Contract",
            "contract.cancel":          "Cancel",
            "contract.waiting_sign":    "📝 Awaiting Signature",
            "contract.waiting_approve": "⏳ Pending Approval",
            "contract.no_data":         "No contracts found in the list.",
            "contract.emp_name":        "Employee Name",
            "contract.modal_title":     "📝 Draft Employment Contract",
            "contract.approve":         "✅ Approve (Finalize)",
            "contract.sign_title":      "✍️ Sign Contract",
            "contract.sign_here":       "Enter your full name to sign...",
            "contract.confirm_sign":    "Confirm Signature",
            "contract.emp_signature":   "Employee Signature:",
            "contract.hr_signature":    "HR Signature:",
            "contract.signed":          "✅ Signed",
            "contract.not_signed":      "Not Signed",

            // === SALARY ===
            "salary.title":             "💰 Payroll",
            "salary.my_salary":         "My Salary History",
            "salary.company_salary":    "Company Payroll",
            "salary.period":            "Pay Period",
            "salary.base":              "Base Salary",
            "salary.allowance":         "Allowance",
            "salary.deduction":         "Deductions & Tax",
            "salary.net":               "Net Salary",
            "salary.gross":             "Gross Income",
            "salary.work_days":         "Work Hours",
            "salary.ot_hours":          "Overtime",
            "salary.calculate":         "Calculate",
            "salary.export":            "Export CSV",
            "salary.print":             "Export PDF / Print",
            "salary.insurance":         "Insurance (10.5%)",
            "salary.tax":               "Personal Income Tax",
            "salary.other_deduction":   "Other",
            "salary.total_income":      "Total Income",
            "salary.print_slip":        "Print Pay Slip",
            "salary.no_data":           "No salary records found.",

            // === PAYROLL PAYMENT ===
            "payment.title":            "💳 Salary Payment",
            "payment.confirm":          "Create Payment Batch",
            "payment.status_paid":      "Paid",
            "payment.status_pending":   "Selected Employees",
            "payment.batch_pay":        "Batch Payment",
            "payment.pay_all":          "Pay All",
            "payment.amount":           "Total Amount (selected)",
            "payment.date":             "Payment Date",
            "payment.method":           "Payment Method",
            "payment.mark_paid":        "Confirm Payment",
            "payment.period":           "Pay Period",
            "payment.dept":             "Department",
            "payment.pay_status":       "Payment Status",
            "payment.bank":             "Bank Account",
            "payment.net_salary":       "Net Salary",
            "payment.all_status":       "-- All Statuses --",
            "payment.paid_badge":       "✅ Paid",
            "payment.unpaid_badge":     "⏳ Pending",
            "payment.processing_badge": "🔄 Processing",
            "payment.no_data":          "No records found.",
            "payment.select_all":       "Select all",
            "payment.no_bank_warn":     "No bank account on file",
            "payment.filter_btn":       "Filter",

            // === NOTIFICATION ===
            "notif.title":              "Notification Management",
            "notif.system_title":       "📢 System Notifications",
            "notif.send_to":            "👤 Send To",
            "notif.content":            "📝 Message Content",
            "notif.send_btn":           "🚀 Send",
            "notif.delete":             "🗑 Delete",
            "notif.recall":             "🔄 Recall",
            "notif.send_all":           "📢 Send to All Employees",
            "notif.send_hr":            "📩 Send to HR",
            "notif.send_admin":         "⚙️ Send to System Admin",
            "notif.send_leader":        "🏗️ Send to Management",
            "notif.personal":           "Send to Specific Employee",
            "notif.empty":              "No notifications for you yet.",
            "notif.from":               "From:",
            "notif.to":                 "To:",
            "notif.placeholder":        "Enter message to broadcast...",
            "notif.online":             "● Online",
            "notif.connecting":         "Connecting Real-time...",
            "notif.recall_confirm":     "Recall notification?",
            "notif.recall_text":        "The message will disappear for all clients!",
            "notif.recall_ok":          "Confirm",
            "notif.sent_success":       "Sent successfully!",
            "notif.send_general":       "General",
            "notif.send_management":    "Management",

            // === REPORT ===
            "report.title":             "📊 HR Summary Report",
            "report.export":            "Export Report",
            "report.period":            "Report Period",
            "report.type":              "Report Type",
            "report.generate":          "Generate Report",
            "report.table_tab":         "Data Table",
            "report.file_tab":          "Attachments",
            "report.print":             "Print Report",

            // === COMMON ===
            "common.save":              "Save",
            "common.cancel":            "Cancel",
            "common.edit":              "Edit",
            "common.delete":            "Delete",
            "common.confirm":           "Confirm",
            "common.close":             "Close",
            "common.search":            "Search",
            "common.filter":            "Filter",
            "common.export":            "Export",
            "common.print":             "Print",
            "common.back":              "Back",
            "common.next":              "Next",
            "common.loading":           "Loading...",
            "common.no_data":           "No data available",
            "common.success":           "Success",
            "common.error":             "An error occurred",
            "common.warning":           "Warning",
            "common.yes":               "Yes",
            "common.no":                "No",
            "common.from":              "From",
            "common.to":                "To",
            "common.actions":           "Actions",
            "common.status":            "Status",
            "common.detail":            "Details",
            "common.total":             "Total",
            "common.stt":               "No.",
            "common.add":               "Add",
            "common.view":              "View",
            "common.reset":             "Reset",
            "common.approve":           "Approve",
            "common.reject":            "Reject",
            "common.time":              "Time",
            "common.category":          "Category",
            "common.event_msg":         "Event / Message",
            "common.need_process":      "ACTION NEEDED",
            "common.system":            "System",
            "common.system_online":     "● HRMS Management System Online",
            "common.connecting":        "Connecting...",
            "common.online":            "● Online",
            "common.from_label":        "FROM:",
            "common.msg_incoming":      "📩 Incoming Message",
            "common.checkin_log":       "⏱ Attendance",
            "common.leave_log":         "📄 Leave",
            "common.contract_log":      "📑 Contract",
            "common.approve_log":       "✅ Approved",
            "common.log_title":         "🔔 Activity Log & Messages from Actors",
            "common.select_all":        "-- All --",
            "common.view_detail":       "View Details",

            // === STATUS ===
            "status.active":            "✅ Active",
            "status.inactive":          "❌ Resigned",
            "status.pending":           "Pending",
            "status.approved":          "Approved",
            "status.rejected":          "Rejected",
            "status.paid":              "Paid",
            "status.unpaid":            "Unpaid",
            "status.official":          "Official",
            "status.probation":         "Probation",
            "status.completed":         "Completed",
            "status.not_checkout":      "Not Checked Out",
            "status.processing":        "Processing",
            "status.working":           "Working",

            // === ACCOUNT / LOGIN ===
            "account.login_title":      "Login - HRMS",
            "account.username":         "Username",
            "account.password":         "Password",
            "account.remember":         "Remember me",
            "account.login_btn":        "Log In",
            "account.forgot":           "Forgot your password?",
            "account.logout":           "Logout",
            "account.err_no_user":      "Please enter your username",
            "account.err_no_pass":      "Please enter your password",
            "account.err_invalid":      "Invalid username or password",

            // === VALIDATION / ERROR MESSAGES ===
            "msg.delete_confirm":       "Are you sure you want to delete?",
            "msg.delete_permanent":     "This record will be permanently deleted!",
            "msg.delete_ok":            "Yes, Delete",
            "msg.invalid_data":         "Invalid data",
            "msg.not_found":            "No data found",
            "msg.save_success":         "Saved successfully",
            "msg.save_error":           "An error occurred",
            "msg.system_added":         "has been added to the list!",
            "msg.list_empty":           "List is currently empty.",
            "msg.contract_approved":    "has been approved and is now an official employee.",
            "msg.welcome_new":          "🎉 Congratulations! has officially joined the team.",
            "msg.checkin_done":         "just checked in",
            "msg.leave_submitted":      "just submitted a leave request",
            "msg.contract_requested":   "requested contract signing",
            "msg.new_msg_from":         "📩 New message from",

            // === AI ASSISTANT ===
            "ai.title":                 "🤖 HRMS Assistant",
            "ai.reset":                 "Reset",
            "ai.placeholder":           "Ask about salary, employees...",
            "ai.send":                  "Send",
            "ai.greeting":              "Hello! I'm your AI assistant. How can I help you with HR today?",
            "ai.thinking":              "🤖 Thinking...",
            "ai.error":                 "⚠️ Server error, please try again!",
            "ai.reset_msg":             "Chat history cleared! How can I help you?",

            // === PERMISSION / ADMIN ===
            "perm.title":               "🛡 Permission Management",
            "perm.role":                "Role",
            "perm.assign":              "Assign Role",
            "perm.hr":                  "HR",
            "perm.admin":               "Admin",
            "perm.employee":            "Employee",
            "perm.leader":              "Manager",
        }
    };

    // ============================================================
    // HÀM LẤY NGÔN NGỮ HIỆN TẠI
    // ============================================================
    function getLang() {
        try {
            return localStorage.getItem('hrms_lang') || 'vi';
        } catch (e) {
            return 'vi';
        }
    }

    // ============================================================
    // HÀM LƯU NGÔN NGỮ
    // ============================================================
    function setLang(lang) {
        try {
            localStorage.setItem('hrms_lang', lang);
        } catch (e) { /* localStorage không khả dụng */ }
    }

    // ============================================================
    // HÀM DỊCH MỘT KEY
    // ============================================================
    function t(key, lang) {
        lang = lang || getLang();
        const dict = translations[lang];
        if (dict && dict[key] !== undefined) return dict[key];
        // Fallback về tiếng Việt
        if (lang !== 'vi' && translations['vi'] && translations['vi'][key] !== undefined) {
            return translations['vi'][key];
        }
        return null; // Không tìm thấy → giữ nguyên text gốc
    }

    // ============================================================
    // HÀM ÁP DỤNG NGÔN NGỮ CHO TOÀN TRANG
    // ============================================================
    function applyLanguage(lang) {
        lang = lang || getLang();

        // Xử lý tất cả element có data-i18n (textContent)
        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            var translated = t(key, lang);
            if (translated !== null) {
                el.textContent = translated;
            }
        });

        // Xử lý element có data-i18n-html (innerHTML - cho icon + text)
        document.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-html');
            var translated = t(key, lang);
            if (translated !== null) {
                el.innerHTML = translated;
            }
        });

        // Xử lý placeholder
        document.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-placeholder');
            var translated = t(key, lang);
            if (translated !== null) {
                el.setAttribute('placeholder', translated);
            }
        });

        // Xử lý title/tooltip
        document.querySelectorAll('[data-i18n-title]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-title');
            var translated = t(key, lang);
            if (translated !== null) {
                el.setAttribute('title', translated);
            }
        });

        // Xử lý option text (data-i18n-opt trên <option>)
        document.querySelectorAll('[data-i18n-opt]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-opt');
            var translated = t(key, lang);
            if (translated !== null) {
                el.textContent = translated;
            }
        });

        // Xử lý optgroup label (data-i18n-group trên <optgroup>)
        document.querySelectorAll('[data-i18n-group]').forEach(function (el) {
            var key = el.getAttribute('data-i18n-group');
            var translated = t(key, lang);
            if (translated !== null) {
                el.setAttribute('label', translated);
            }
        });

        // Cập nhật nút ngôn ngữ
        var langBtn = document.getElementById('hrms-lang-btn-text');
        if (langBtn) {
            langBtn.textContent = lang === 'vi' ? '🇻🇳 Tiếng Việt' : '🇬🇧 English';
        }

        // Lưu vào localStorage
        setLang(lang);

        // Dispatch event để các script khác lắng nghe
        try {
            window.dispatchEvent(new CustomEvent('hrms:langChanged', { detail: { lang: lang } }));
        } catch (e) { /* ignore */ }
    }

    // ============================================================
    // CHUYỂN ĐỔI NGÔN NGỮ
    // ============================================================
    function switchLanguage(lang) {
        applyLanguage(lang);
    }

    // ============================================================
    // AUTO-APPLY KHI TRANG LOAD
    // ============================================================
    function init() {
        var lang = getLang();
        // Đợi DOM ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () {
                applyLanguage(lang);
            });
        } else {
            applyLanguage(lang);
        }
    }

    // Khởi động ngay
    init();

    // ============================================================
    // PUBLIC API
    // ============================================================
    return {
        t: t,
        getLang: getLang,
        setLang: setLang,
        applyLanguage: applyLanguage,
        switchLanguage: switchLanguage,
        translations: translations
    };

})();
