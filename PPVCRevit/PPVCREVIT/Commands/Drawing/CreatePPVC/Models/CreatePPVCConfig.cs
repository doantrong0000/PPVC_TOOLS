namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreatePPVCConfig
    {
        // Tên Parameter phân loại Rebar
        public const string RebarTypeParamName = "WH_Rebar_Type";

        // Giá trị Parameter phân loại Rebar cần lọc
        public const string RebarTypeParamValue = "WALL LOOP";

        // Từ khóa tìm kiếm Family Wall Tag
        public const string WallTagKeyword = "WallTag";

        // Cấu hình Rebar Tag: 1 Family có nhiều Types
        public static class RebarTag
        {
            public const string FamilyName = "WH_RebarTag_v26";

            // Danh sách các Type của Rebar Tag
            public const string Type1 = "";
            public const string Type2 = "";
            public const string Type3 = "10-H20-200-TEXT";
            public const string Type4 = "10-H20-TEXT"; // WALL LOOP TAG

            // Danh sách từ khoá hoặc tên type cho MultiReferenceAnnotation (Multi-tag)
            // Ưu tiên chọn Type có tên chứa các chuỗi sau (Ví dụ: "1_Multi_tag_for_SlabRebar_BaseView")
            public static readonly string[] MRATypes = new string[]
            {
                "1_"
            };
        }
    }
}
