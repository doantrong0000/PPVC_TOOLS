using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tekla.Structures.Model;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TestTekla.Models
{
    public static class RebarMethol
    {
        public static RebarGroup CopyRebar(RebarGroup rebar)
        {
            // 1. Tạo một đối tượng mới cùng kiểu
            RebarGroup newRebar = new RebarGroup();

            // 2. Copy các thuộc tính cơ bản
            newRebar.Name = rebar.Name;
            newRebar.Class = rebar.Class;
            newRebar.NumberingSeries = rebar.NumberingSeries;

            string rSize = ""; string rGrade = "";
            rebar.GetReportProperty("SIZE", ref rSize);
            rebar.GetReportProperty("GRADE", ref rGrade);
            newRebar.Size = rSize;
            newRebar.Grade = rGrade;
            newRebar.RadiusValues = rebar.RadiusValues;

            // ---------------------------------------------------------
            // CÁC THUỘC TÍNH BẮT BUỘC PHẢI CÓ ĐỂ INSERT THÀNH CÔNG
            // ---------------------------------------------------------

            // Bắt buộc: Gán Part chủ quản cho thanh thép mới
            newRebar.Father = rebar.Father;

            // Bắt buộc đối với RebarGroup: Đường phân bố của nhóm thép
            newRebar.StartPoint = rebar.StartPoint;
            newRebar.EndPoint = rebar.EndPoint;

            // Bắt buộc: Các thông số khoảng cách rải thép
            newRebar.SpacingType = rebar.SpacingType;
            newRebar.Spacings = rebar.Spacings;
            newRebar.ExcludeType = rebar.ExcludeType;

            // ---------------------------------------------------------
            // CÁC THUỘC TÍNH OFFSET (Giúp thanh thép mới nằm đúng vị trí cũ)
            // ---------------------------------------------------------
            newRebar.StartPointOffsetType = rebar.StartPointOffsetType;
            newRebar.StartPointOffsetValue = rebar.StartPointOffsetValue;
            newRebar.EndPointOffsetType = rebar.EndPointOffsetType;
            newRebar.EndPointOffsetValue = rebar.EndPointOffsetValue;

            newRebar.StartFromPlaneOffset = rebar.StartFromPlaneOffset;
            newRebar.EndFromPlaneOffset = rebar.EndFromPlaneOffset;
            newRebar.OnPlaneOffsets = rebar.OnPlaneOffsets;

            // Copy Hooks (móc thép)
            newRebar.StartHook = rebar.StartHook;
            newRebar.EndHook = rebar.EndHook;

            return newRebar;
        }
    }
}
