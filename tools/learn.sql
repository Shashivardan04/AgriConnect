SELECT
    B.Id AS BookingId,
    B.ListingId,
    L.Title AS ListingTitle,
    B.RequesterId,
    B.FromDate,
    B.ToDate,
    B.Status,
    B.Message,
    B.CreatedAt
FROM Bookings B
INNER JOIN Listings L
    ON B.ListingId = L.Id
ORDER BY B.CreatedAt DESC;
