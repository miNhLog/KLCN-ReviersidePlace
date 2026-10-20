"""Xuat du lieu review that (DanhGia/DatTiec) ra CSV cho CF.

De cuong CNTT-KLCN257 phan biet ro: hien tai CF hoc tren synthetic
``wedding_dataset_5k.csv``; khi he thong thu du lieu that tu khach (QR review
sau tiec + lich su dat), chay script nay de xuat ``wedding_reviews_real.csv``
voi cung schema toi thieu (SampleId~KhachHangID, HallId/MenuId/DecorId,
Satisfaction~DiemTongThe) roi fit lai ``cf_engine``.

TODO-5: JOIN LichSanh lay SanhTiecID that (khong ghi HallId=0), guard min-rows 20
(cf_engine fallback synthetic khi file that < 20 dong).

Cach dung (tren may co SQL Server + dotnet):
    python app/core/export_reviews.py --connection-string "Server=...;..." --out data/wedding_reviews_real.csv

Neu chua co DB that, script tao file mau header-only va in huong dan.
"""
import argparse
import csv
import logging
import os
import sys
from pathlib import Path
from typing import Optional

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("export_reviews")

BASE = Path(__file__).resolve().parent.parent.parent
OUT_DEFAULT = BASE / "data" / "wedding_reviews_real.csv"
MIN_REAL_ROWS = 20

CSV_COLUMNS = [
    "SampleId",
    "user_id",
    "HallId",
    "MenuId",
    "DecorId",
    "combo_id",
    "Satisfaction",
    "rating",
    "FoodQuality",
    "StaffService",
    "DecorationAtmosphere",
    "CostEffectiveness",
    "comment",
    "created_at",
]

# Truy van dung schema thuc te cua Riverside Palace:
# DanhGia (r) -> MaQRDanhGia (qr) -> DatTiec (dt) -> LichSanh (ls)
QUERY = """
SELECT 
    r.DanhGiaID AS SampleId,
    ISNULL(dt.KhachHangID, 0) AS user_id,
    ISNULL(ls.SanhTiecID, 0) AS HallId,
    ISNULL(dt.ThucDonID, 0) AS MenuId,
    ISNULL(dt.GoiTrangTriID, 0) AS DecorId,
    CAST(r.DiemTongThe AS FLOAT) AS rating,
    CAST(r.DiemTongThe AS FLOAT) AS Satisfaction,
    CAST(ISNULL(r.DiemMonAn, 0) AS FLOAT) AS FoodQuality,
    CAST(ISNULL(r.DiemPhucVu, 0) AS FLOAT) AS StaffService,
    CAST(ISNULL(r.DiemSanh, 0) AS FLOAT) AS DecorationAtmosphere,
    CAST(ISNULL(r.DiemAmThanh, 0) AS FLOAT) AS CostEffectiveness,
    REPLACE(REPLACE(ISNULL(r.BinhLuan, N''), CHAR(13), ' '), CHAR(10), ' ') AS comment,
    CONVERT(VARCHAR(19), r.NgayDanhGia, 120) AS created_at
FROM DanhGia r
INNER JOIN MaQRDanhGia qr ON r.MaQRDanhGiaID = qr.MaQRDanhGiaID
INNER JOIN DatTiec dt ON qr.DatTiecID = dt.DatTiecID
LEFT JOIN LichSanh ls ON dt.LichSanhID = ls.LichSanhID
WHERE r.DiemTongThe IS NOT NULL
ORDER BY r.NgayDanhGia DESC;
"""

CANDIDATE_CONNECTION_STRINGS = [
    # 1. Local SQLEXPRESS with Windows Authentication (ODBC 17)
    r"Driver={ODBC Driver 17 for SQL Server};Server=.\SQLEXPRESS;Database=IRiversidePalaceDB;Trusted_Connection=yes;TrustServerCertificate=yes;",
    # 2. Local SQLEXPRESS with Windows Authentication (ODBC 18)
    r"Driver={ODBC Driver 18 for SQL Server};Server=.\SQLEXPRESS;Database=IRiversidePalaceDB;Trusted_Connection=yes;TrustServerCertificate=yes;",
    # 3. Docker / localhost with sa credentials
    r"Driver={ODBC Driver 17 for SQL Server};Server=localhost,1433;Database=IRiversidePalaceDB;UID=sa;PWD=Riverside@12345;TrustServerCertificate=yes;",
    # 4. Default instance (.)
    r"Driver={ODBC Driver 17 for SQL Server};Server=.;Database=IRiversidePalaceDB;Trusted_Connection=yes;TrustServerCertificate=yes;",
]


def detect_connection_string(explicit_str: Optional[str] = None) -> Optional[str]:
    """Tu dong tim connection string hoat dong toi CSDL SQL Server."""
    if explicit_str and explicit_str.strip():
        return explicit_str.strip()

    env_str = os.getenv("DB_CONNECTION_STRING") or os.getenv("SQL_CONNECTION_STRING")
    if env_str and env_str.strip():
        return env_str.strip()

    try:
        import pyodbc
    except ImportError:
        logger.error("Chua cai dat thu vien pyodbc. Vui long chay: pip install pyodbc")
        return None

    for conn_str in CANDIDATE_CONNECTION_STRINGS:
        try:
            con = pyodbc.connect(conn_str, timeout=3)
            con.close()
            logger.info(f"Ket noi SQL Server thanh cong qua: {conn_str.split(';')[1]}")
            return conn_str
        except Exception:
            continue

    return None


def export_via_odbc(conn_str: str, out_path: Path) -> int:
    try:
        import pyodbc
    except ImportError:
        logger.error("pyodbc chua duoc cai dat. Vui long chay: pip install pyodbc")
        return 0

    out_path.parent.mkdir(parents=True, exist_ok=True)
    con = pyodbc.connect(conn_str)
    cur = con.cursor()
    cur.execute(QUERY)
    rows = cur.fetchall()

    kept = 0
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(CSV_COLUMNS)
        for r in rows:
            sample_id = int(r[0])
            user_id = int(r[1])
            hall_id = int(r[2])
            menu_id = int(r[3])
            decor_id = int(r[4])
            rating = float(r[5])
            satisfaction = float(r[6])
            food = float(r[7])
            service = float(r[8])
            atmosphere = float(r[9])
            cost = float(r[10])
            comment = str(r[11])
            created_at = str(r[12])

            combo_id = f"{hall_id}_{menu_id}_{decor_id}"

            w.writerow([
                sample_id,
                user_id,
                hall_id,
                menu_id,
                decor_id,
                combo_id,
                satisfaction,
                rating,
                food,
                service,
                atmosphere,
                cost,
                comment,
                created_at,
            ])
            kept += 1

    con.close()

    if kept < MIN_REAL_ROWS:
        logger.warning(
            f"[export_reviews] CANH BAO: Chi trich xuat duoc {kept} dong danh gia (< {MIN_REAL_ROWS}). "
            f"CF Engine se tu dong fallback sang du lieu synthetic (wedding_dataset_5k.csv)."
        )
    else:
        logger.info(
            f"[export_reviews] Da xuat thanh cong {kept} dong danh gia thuc te (>= {MIN_REAL_ROWS}). "
            f"CF Engine san sang huan luyen tren du lieu that."
        )

    return kept


def main() -> None:
    ap = argparse.ArgumentParser(description="Trich xuat DanhGia that tu SQL Server ra CSV cho CF Engine.")
    ap.add_argument("--connection-string", default="", help="Chuoi ket noi ODBC SQL Server")
    ap.add_argument("--out", default=str(OUT_DEFAULT), help="Duong dan file CSV xuat ra")
    args = ap.parse_args()

    out = Path(args.out)
    conn_str = detect_connection_string(args.connection_string)

    if not conn_str:
        logger.warning(
            f"Khong the tu dong ket noi toi SQL Server. Giu file CSV hien tai tai {out}.\n"
            f"Ban co the chi dinh qua tham so: python export_reviews.py --connection-string \"...\""
        )
        return

    n = export_via_odbc(conn_str, out)
    print(f"[export_reviews] Da trich xuat xong {n} dong du lieu danh gia -> {out}")


if __name__ == "__main__":
    main()

