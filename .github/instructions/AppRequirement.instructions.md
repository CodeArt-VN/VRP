---
applyTo: '**'
---

# Yêu cầu ứng dụng
Bạn là một chuyên gia lập trình C# với kinh nghiệm xây dựng hệ thống logistics tối ưu. Tôi muốn bạn viết một API hoàn chỉnh bằng **C# .NET Core 9.0** để tự động hóa quy trình **phân tài giao hàng** (vehicle routing and assignment) cho một hệ thống logistics. API cần được thiết kế tối ưu, dễ mở rộng, tuân thủ các nguyên tắc SOLID, RESTful, sử dụng các pattern như Repository, Dependency Injection, và có khả năng xử lý các nghiệp vụ phức tạp. Hãy sử dụng **Entity Framework Core** để quản lý cơ sở dữ liệu (SQL Server) và thêm các tính năng như logging, exception handling, unit test, và documentation (Swagger).

**Yêu cầu nghiệp vụ:**

Tôi có một hệ thống logistics với các thành phần sau:

1.  **Xe giao hàng (Vehicle)**: Mỗi xe có thông tin:
    *   Id, Code, Name, kích thước (chiều dài, chiều rộng, chiều cao).
    *   Loại xe (e.g., xe máy, xe tải nhỏ, xe tải lớn, xe 5T, xe đông lạnh 5T).
    *   Thể tích tối thiểu, khuyến nghị và tối đa (`MaxVolume`).
    *   Trọng tải tối thiểu, khuyến nghị và tối đa (`MaxWeight`).
    *   Khu vực hoạt động (có thể giới hạn bởi quận/huyện), các đường không được phép di chuyển.
    *   Tình trạng xe hiện tại (`CurrentStatus`) - *Lưu ý: Không dùng cho yêu cầu phân tài ban đầu `api/Routes/Calc`.*
    *   Trọng tải còn lại (`RemainingWeight`) - *Lưu ý: Không dùng cho yêu cầu phân tài ban đầu `api/Routes/Calc`.*
    *   Thể tích còn lại (`RemainingVolume`) - *Lưu ý: Không dùng cho yêu cầu phân tài ban đầu `api/Routes/Calc`.*
    *   Vị trí hiện tại (`CurrentLocation` as `NetTopologySuite.Geometries.Point?`) - *Lưu ý: Không dùng cho yêu cầu phân tài ban đầu `api/Routes/Calc`. Xe sẽ bắt đầu từ kho.* 
2.  **Danh sách Địa chỉ (Address)**:
    *   Id, Name, Phone, District, Province, Ward, Street, Address1, Address2.
    *   Vị trí: `Location` as `NetTopologySuite.Geometries.Point?` (thay cho Lat, Long).
3.  **Kho hàng (Depot)** và **Hub trung chuyển**:
    *   Id, Name, IDAddress
    *   IDAddress để link với danh sách địa chỉ để lấy ra được tọa độ (`Point`).
4.  **Đơn hàng giao (DeliveryOrder)**:
    *   Id, IDAddress, DeliveryType, OrderLines.
    *   IDAddress để link với địa chỉ để lấy được tọa độ.
    *   Danh sách item (Id, Item, Quantity, `Weight`, `Volume`).
    *   DeliveryType (Yêu cầu loại xe giao hàng mẫu trưng bày, khẩn cấp, hàng bán sỉ, giao bình thường).
    *   Thời gian yêu cầu giao (Deadline).
    *   Mức độ ưu tiên (thấp/trung bình/cao).
5.  **Đơn hàng lấy hàng (PickupOrder)**: *(Tạm thời chưa triển khai trong API phân tài ban đầu)*
    *   Id, IDAddress
    *   IDAddress để link với địa chỉ lấy hàng của Seller.
    *   Danh sách item (tên, số lượng, cân nặng, thể tích).
    *   Đích đến (Depot hoặc Hub).
    *   Thời gian yêu cầu lấy (deadline nếu có).
6.  **Dữ liệu index trong quá khứ IndexDistance:**
    *   **DEFERRED/REMOVED:** Chức năng này đã được hoãn lại. Việc tính toán khoảng cách sẽ được thực hiện bởi `DistanceService` sử dụng tọa độ `Point` (ví dụ: Haversine formula), không dựa vào bảng `IndexDistance` đã cache sẵn cho logic `api/Routes/Calc` ban đầu.

**Quy trình hiện tại:**

Hiện tại, nhân viên phải xem danh sách đơn hàng (giao và lấy) và dựa vào kinh nghiệm để quyết định:

*   Xe nào sẽ giao đơn hàng nào.
*   Xe nào sẽ lấy hàng từ Seller để mang về kho/hub.
*   Sau khi phân tài, các xe đến kho/hub để nhận hàng, giao hàng theo lịch trình.
*   Trong quá trình di chuyển Nếu xe còn chỗ trống để chứa hàng thì xe có thể được chỉ định lấy hàng từ Seller (đơn hàng lấy).

**Yêu cầu mở các API:**

1.  **Tự động phân tài giao hàng (`POST api/Routes/Calc`)**:
    *   Controller: `RoutesController.cs`
    *   Method: `CalculateRoutes(VehicleRoutingRequest request)`
    *   Đầu vào (`VehicleRoutingRequest.cs`):

        ```json
        {
          "Vehicles": [
            {
              "Id": 1,
              "Code": "XE-001",
              "Name": "Xe Tải 1.5 Tấn",
              "Type": "1.5_TON_TRUCK",
              "MaxVolume": 10.0,
              "MaxWeight": 1500.0,
              "OptimalVolume": 8.0,
              "OptimalWeight": 1200.0
            }
          ],
          "Orders": [
            {
              "Id": 101,
              "IDAddress": 2,
              "DeliveryType": "NORMAL",
              "OrderLines": [
                {
                  "Item": "Item A",
                  "Quantity": 5,
                  "Weight": 50.0,
                  "Volume": 0.5
                }
              ],
              "Deadline": "2024-12-31T17:00:00Z",
              "Priority": "MEDIUM"
            }
          ],
          "IDDepotAddress": 1
        }
        ```

    *   Đầu ra (`VehicleRoutingResponse.cs`):

        ```json
        {
          "NumberOfOptions": 1,
          "Options": [
            {
              "OptionName": "Default Optimization",
              "Shipments": [
                {
                  "IDVehicle": 1,
                  "IDDepotAddress": 1,
                  "Route": [
                    { "IDAddress": 2, "IDOrder": 101, "Sequence": 1, "Latitude": 10.0, "Longitude": 20.0 }
                  ],
                  "TotalRouteDistance": 75.5,
                  "TotalRouteTime": "PT4H30M"
                }
              ],
              "UnassignedOrders": []
            }
          ]
        }
        ```

    *   Yêu cầu xử lý: Xây dựng thuật toán tối ưu để phân bổ đơn giao hàng cho các xe dựa trên:
        *   Khoảng cách của các địa chỉ, tính toán bằng `DistanceService`.
        *   Dung tích (`MaxVolume`) và tải trọng (`MaxWeight`) của xe.
        *   Thời gian yêu cầu giao hàng (`Deadline`).
		*   Tối ưu hóa lộ trình (ví dụ: giảm quãng đường, thời gian). Xe bắt đầu từ `IDDepotAddress` và quay về đó.
        *   Mở rộng để tính phương án lấy hàng sau. Tạm thời viết một hàm trống trước cho phần `Picking`.
2.  **Quản lý Address**:
    *   CRUD cho danh address (đã có `AddressController`).
    *   API lập index khoảng cách: **DEFERRED/REMOVED**. Thay vào đó, `DistanceService` sẽ cung cấp các hàm tính khoảng cách cần thiết.
        *   Hàm tính khoảng cách 2 điểm dựa trên tọa độ `Point` (e.g., Haversine) sẽ nằm trong `DistanceService.cs`.
        *   Việc gọi Google Maps API để lấy quãng đường thực tế có thể được thêm vào `DistanceService` sau này nếu cần, nhưng không phải là ưu tiên cho `api/Routes/Calc` ban đầu.

**Ràng buộc bổ sung (thực tế):**

(Giữ nguyên các ràng buộc này, nhưng lưu ý rằng không phải tất cả sẽ được triển khai ngay trong giai đoạn đầu của `api/Routes/Calc`)

*   **Thời gian làm việc**
*   **Giới hạn địa lý**
*   **Chi phí**
*   **Tính sẵn sàng**
*   **Hàng hóa đặc biệt**
*   **Tính mở rộng**
*   **Bảo mật**
*   **Hiệu suất**

**Công nghệ yêu cầu:**

*   **Framework**: .NET Core 9.0.
*   **Database**: SQL Server, sử dụng Entity Framework Core với `NetTopologySuite`.
*   **Services**: `DistanceService.cs` để tính toán khoảng cách.
*   **Logging**: Serilog.
*   **API Documentation**: Swagger.
*   **Containerization**: Cung cấp Dockerfile để triển khai trên Docker.

**Kết quả mong muốn:**

1.  Source code hoàn chỉnh, chạy được, với các comment rõ ràng.
2.  File `README.md` hướng dẫn cách cài đặt, chạy, và test API.
3.  Một số test case mẫu (unit test và integration test).
4.  Tài liệu mô tả thuật toán phân tài (e.g., sử dụng thuật toán gì: heuristic approach for now).
5.  Dockerfile và docker-compose.yml để triển khai.

**Lưu ý:**

*   Nếu có bất kỳ giả định nào, hãy nêu rõ trong code hoặc README.
*   Đảm bảo API có thể xử lý các trường hợp lỗi (e.g., xe không đủ, đơn hàng không hợp lệ).
*   Tối ưu hóa hiệu suất là ưu tiên hàng đầu.
*   Vui lòng:
    *   Viết code đầy đủ, có chú thích rõ ràng từng hàm và đoạn code.
    *   Bao gồm file `README.md` hướng dẫn cách cài đặt, chạy, và sử dụng phần mềm.
    *   Viết các test case đơn giản để kiểm tra tính đúng đắn của thuật toán phân bổ.
    *   Dùng code theo chuẩn và dễ bảo trì.


# Folder chứa project chính là `SmartRouting`.

# Kế hoạch và tiến độ thực Hiện
* Hãy theo dõi tiến độ thực hiện trong file [BACKLOG.md](BACKLOG.md).