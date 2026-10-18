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
import sys
from pathlib import Path

BASE = Path(__file__).resolve().parent.parent.parent
OUT_DEFAULT = BASE / "data" / "wedding_reviews_real.csv"
HEADER = ["SampleId", "HallId", "MenuId", "DecorId", "Satisfaction"]
MIN_REAL_ROWS = 20

# TODO-5: dung ten bang/cot that (DanhGia/MaQRDanhGia/DatTiec/LichSanh/KhachHang),
# JOIN LichSanh de lay SanhTiecID that thay vi ghi 0.
QUERY = """
SELECT c.KhachHangID AS SampleId, ls.SanhTiecID AS HallId, b.ThucDonID AS MenuId,
       b.GoiTrangTriID AS DecorId, r.DiemTongThe AS Satisfaction
FROM DanhGia r
JOIN MaQRDanhGia q ON q.MaQRDanhGiaID = r.MaQRDanhGiaID
JOIN DatTiec b ON b.DatTiecID = q.DatTiecID
JOIN LichSanh ls ON ls.LichSanhID = b.LichSanhID
JOIN KhachHang c ON c.KhachHangID = b.KhachHangID
WHERE r.DiemTongThe IS NOT NULL;
"""


def export_via_odbc(conn_str: str, out_path: Path) -> int:
    try:
        import pyodbc  # type: ignore
    except Exception:
        print("pyodbc chua cai: pip install pyodbc. Ghi file header-only lam mau.", file=sys.stderr)
        with open(out_path, "w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerow(HEADER)
        return 0
    con = pyodbc.connect(conn_str)
    cur = con.cursor()
    cur.execute(QUERY)
    rows = cur.fetchall()
    kept = 0
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(HEADER)
        for r in rows:
            hall_id, menu_id, decor_id = r[1], r[2], r[3]
            if hall_id is None or menu_id is None or decor_id is None:
                continue
            try:
                w.writerow([int(r[0]), int(hall_id), int(menu_id), int(decor_id), float(r[4])])
            except (TypeError, ValueError):
                continue
            kept += 1
    con.close()
    if kept < MIN_REAL_ROWS:
        print(f"[export_reviews] CANH BAO: chi co {kept} dong that (< {MIN_REAL_ROWS}); "
              f"cf_engine se fallback synthetic cho den khi du lieu that.",
              file=sys.stderr)
    return kept


def main() -> None:
    ap = argparse.ArgumentParser(description="Export DanhGia that ra CSV cho CF.")
    ap.add_argument("--connection-string", default="", help="ODBC/SQL Server connection string")
    ap.add_argument("--out", default=str(OUT_DEFAULT))
    args = ap.parse_args()
    out = Path(args.out)
    if not args.connection_string:
        out.parent.mkdir(parents=True, exist_ok=True)
        if not out.exists():
            with open(out, "w", newline="", encoding="utf-8") as f:
                csv.writer(f).writerow(HEADER)
        print(f"[export_reviews] Chua co connection-string: giu file mau {out}. "
              f"Khi co du lieu that, chay lai voi --connection-string.")
        print(f"[export_reviews] Query tham khao:\n{QUERY}")
        return
    n = export_via_odbc(args.connection_string, out)
    print(f"[export_reviews] Da xuat {n} dong that -> {out}")


if __name__ == "__main__":
    main()
