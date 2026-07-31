using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Utils
{
    public static class ParameterUtils
    {
        /// <summary>
        /// Tìm parameter theo tên và gán giá trị cho đối tượng
        /// </summary>
        /// <param name="element">Đối tượng Revit cần gán</param>
        /// <param name="paramName">Tên của Parameter</param>
        /// <param name="value">Giá trị cần gán</param>
        /// <returns>Trả về true nếu gán thành công, false nếu thất bại</returns>
        public static bool SetParameterValueByName(Element element, string paramName, object value)
        {
            // 1. Kiểm tra đầu vào
            if (element == null || string.IsNullOrEmpty(paramName) || value == null)
                return false;

            // 2. Tìm parameter theo tên
            Parameter param = element.LookupParameter(paramName);

            // Nếu không tìm thấy parameter hoặc parameter bị khóa (chỉ đọc) thì bỏ qua
            if (param == null || param.IsReadOnly)
                return false;

            try
            {
                // 3. Gán giá trị dựa theo kiểu dữ liệu của Parameter trong Revit
                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(value.ToString());
                        break;

                    case StorageType.Double:
                        param.Set(Convert.ToDouble(value));
                        break;

                    case StorageType.Integer:
                        param.Set(Convert.ToInt32(value));
                        break;

                    case StorageType.ElementId:
                        if (value is ElementId elementId)
                        {
                            param.Set(elementId);
                        }
                        break;

                    case StorageType.None:
                    default:
                        return false;
                }
                return true; // Gán thành công
            }
            catch (Exception)
            {
                // Bắt lỗi nếu giá trị truyền vào không thể convert sang kiểu của Parameter
                return false;
            }
        }
    }
}
