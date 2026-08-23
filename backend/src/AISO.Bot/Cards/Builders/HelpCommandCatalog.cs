using AISO.Domain.Users;

namespace AISO.Bot.Cards.Builders;

/// <summary>
/// Single source of truth for the commands rendered in the help card.
/// Each entry describes one user-intent, its spoken/written patterns (EN + VI),
/// the roles allowed to invoke it, and the flow step it belongs to so the card
/// can be ordered consistently with the user journey.
/// </summary>
internal sealed record HelpCommand(
    string Flow,         // "browse" | "detail" | "act" | "kpi" | "admin" | "session"
    int Step,         // ordering inside a flow
    string Icon,         // short glyph (Teams TextBlock friendly)
    string En,           // primary English phrase
    string Vi,           // primary Vietnamese phrase
    UserRole MinRole,
    UserRole MaxRole = UserRole.Admin,
    string? Note = null  // optional hint shown next to the row
)
{
    public bool Allows(UserRole role) => role >= MinRole && role <= MaxRole;
}

internal static class HelpCommandCatalog
{
    // Roles below Employee exist for future, but only Employee/Manager/Admin are produced today.
    public static IReadOnlyList<HelpCommand> ForRole(UserRole role)
    {
        var all = new[]
        {
            // -------- browse: find orders --------
            new HelpCommand("browse", 1, "•",
                "recent orders",          "đơn hàng gần đây",         UserRole.Employee, UserRole.Employee),
            new HelpCommand("browse", 2, "•",
                "show my sales orders",   "đơn của tôi",              UserRole.Employee),
            new HelpCommand("browse", 3, "•",
                "show open orders",       "đơn đang mở",              UserRole.Employee, UserRole.Manager),
            new HelpCommand("browse", 4, "•",
                "show orders of {SalesOrg}", "đơn của SalesOrg {S}", UserRole.Employee),
            new HelpCommand("browse", 5, "•",
                "show open orders of {SalesOrg}", "đơn mở của {S}", UserRole.Employee, UserRole.Manager),
            new HelpCommand("browse", 6, "•",
                "show overdue orders",    "đơn quá hạn",              UserRole.Employee),
            new HelpCommand("browse", 7, "•",
                "show pending approvals", "chờ duyệt",                UserRole.Manager),

            // -------- detail: look at one --------
            new HelpCommand("detail", 1, "•",
                "show order {N}",         "kiểm tra đơn {N}",         UserRole.Employee),
            new HelpCommand("detail", 2, "•",
                "check order status {N}", "trạng thái đơn {N}",       UserRole.Employee),

            // -------- act: change an order --------
            new HelpCommand("act", 1, "•",
                "create order",           "tạo đơn",                  UserRole.Employee, Note: "up to 5 materials"),
            new HelpCommand("act", 2, "•",
                "edit order {N}",         "sửa đơn {N}",              UserRole.Employee, Note: "only own orders"),
            new HelpCommand("act", 3, "•",
                "update reference {N} to 'PO-99'", "đổi reference {N} thành 'PO-99'", UserRole.Employee),
            new HelpCommand("act", 4, "•",
                "request release {N}",    "xin duyệt {N} · yêu cầu duyệt {N}", UserRole.Employee, Note: "own orders only"),
            new HelpCommand("act", 5, "•",
                "reject order {N}",       "từ chối đơn {N}",          UserRole.Employee, Note: "own orders only"),
            new HelpCommand("act", 6, "•",
                "forward order {N} to DEV-xxx", "chuyển đơn {N} cho DEV-xxx", UserRole.Employee, Note: "own orders only"),
            new HelpCommand("act", 7, "•",
                "approve order {N}",      "duyệt đơn {N} · phê duyệt {N}", UserRole.Manager),
            new HelpCommand("act", 8, "•",
                "reject approval {N}",    "không duyệt {N} · từ chối duyệt {N}", UserRole.Manager),
            new HelpCommand("act", 9, "•",
                "cancel order {N}",       "hủy đơn {N}",              UserRole.Manager),
            new HelpCommand("act", 10, "•",
                "reassign owner {N} to DEV-xxx", "chuyển owner {N} cho DEV-xxx", UserRole.Manager),

            // -------- kpi: reporting --------
            new HelpCommand("kpi", 1, "•",
                "show revenue kpi",       "doanh thu KPI",            UserRole.Employee),
            new HelpCommand("kpi", 2, "•",
                "kpi by customer {K}",    "KPI theo khách {K}",       UserRole.Employee),
            new HelpCommand("kpi", 3, "•",
                "kpi by product {M}",     "KPI theo sản phẩm {M}",    UserRole.Employee),

            // -------- admin: user & audit & force --------
            new HelpCommand("admin", 1, "•",
                "list users",             "danh sách user",           UserRole.Admin),
            new HelpCommand("admin", 2, "•",
                "manage user DEV-xxx",    "quản lý user DEV-xxx",     UserRole.Admin, Note: "set role / sales org"),
            new HelpCommand("admin", 3, "•",
                "view audit log",         "nhật ký audit",            UserRole.Admin),
            new HelpCommand("admin", 4, "•",
                "force release {N}",      "ép release {N}",           UserRole.Admin, Note: "emergency"),
            new HelpCommand("admin", 5, "•",
                "force cancel {N}",       "ép hủy {N}",               UserRole.Admin, Note: "emergency"),

            // -------- delegation: approval proxy --------
            new HelpCommand("admin", 6, "•",
                "delegate approval to DEV-xxx", "ủy quyền duyệt cho DEV-xxx", UserRole.Manager,
                Note: "from {date} to {date}, optional max amount"),
            new HelpCommand("admin", 7, "•",
                "list my delegations",    "danh sách ủy quyền của tôi", UserRole.Manager),
            new HelpCommand("admin", 8, "•",
                "revoke delegation for DEV-xxx", "thu hồi ủy quyền của DEV-xxx", UserRole.Manager),

            // -------- session --------
            new HelpCommand("session", 1, "•",
                "help · hướng dẫn",       "trợ giúp · hướng dẫn",     UserRole.Employee),
            new HelpCommand("session", 2, "•",
                "logout",                 "đăng xuất",                UserRole.Employee),
            new HelpCommand("session", 3, "•",
                "cancel · thoát",         "hủy phiên hiện tại",       UserRole.Employee),
        };

        return all
            .Where(c => c.Allows(role))
            .OrderBy(c => FlowOrder(c.Flow))
            .ThenBy(c => c.Step)
            .ThenBy(c => c.En, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int FlowOrder(string flow) => flow switch
    {
        "browse" => 1,
        "detail" => 2,
        "act" => 3,
        "kpi" => 4,
        "admin" => 5,
        "session" => 6,
        _ => 99,
    };
}
