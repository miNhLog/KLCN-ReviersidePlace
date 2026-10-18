FROM python:3.10-slim
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY . ./
# UC10: bake model embedding (all-MiniLM-L6-v2) vao image de RAG chay offline.
# RAG_EMBEDDINGS rong -> dung semantic; dat tfidf/hash de bo qua model nay.
ENV HF_HOME=/app/.cache/hf
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('all-MiniLM-L6-v2')"
EXPOSE 8000
ENV CATALOG_PATH=/app/data/raw_catalog.json
# TODO-7: HEALTHCHECK sau (catalog + weights) qua /health.
HEALTHCHECK --interval=20s --timeout=6s --retries=5 CMD python -c "import json,urllib.request; h=json.load(urllib.request.urlopen('http://localhost:8000/health',timeout=5)); assert h.get('status')=='ok' and h.get('catalog_loaded') and h.get('weights_source'), h"
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
