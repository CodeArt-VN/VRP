INSERT INTO tbl_001 (Id, Location, Name, Phone, District, Province, Ward, Street, Address1)
SELECT
    c.Id,                                     -- Lấy Id từ tbl_Contact
    geography::Point(c.Long, c.Lat, 4326),    -- Tạo điểm GEOGRAPHY từ Long, Lat với SRID 4326
    NULL,                                     -- Giả sử Name không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL,                                     -- Giả sử Phone không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL,                                     -- Giả sử District không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL,                                     -- Giả sử Province không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL,                                     -- Giả sử Ward không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL,                                     -- Giả sử Street không có trong tbl_Contact hoặc bạn muốn để NULL
    NULL                                      -- Giả sử Address1 không có trong tbl_Contact hoặc bạn muốn để NULL
FROM
    tbl_Contact c;