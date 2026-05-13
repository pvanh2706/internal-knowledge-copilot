# Smart AI Upgrade Plan

Ngay lap: 2026-05-12

Tai lieu nay de xuat cach nang cap Internal Knowledge Copilot tu MVP RAG mock thanh mot he thong tri thuc noi bo thong minh hon, co LLM API cau hinh duoc, retrieval dung quyen, co kha nang tu danh gia loi va cai thien co kiem soat sau moi feedback sai.

## 0. Trang thai trien khai

Cap nhat: 2026-05-13

- Phase 1 `Provider va embedding that`: Done.
  - Da them `AiProviderOptions`.
  - Da them provider `openai` / `openai-compatible` qua `OpenAiCompatibleClient`.
  - Da them real embedding, real answer generation va real wiki draft generation implementation.
  - Van giu `mock` lam mac dinh de local/test khong can API key.
  - Da cap nhat `appsettings.json` va `.env.example`.
- Phase 2 `Permission-aware retrieval`: Done.
  - Da them `KnowledgeQueryFilter`.
  - Da mo rong `IKnowledgeVectorStore.QueryAsync` de nhan metadata filter.
  - Da them Chroma `where` filter theo `source_type`, `status`, `folder_id`, `document_id`, `visibility_scope`.
  - `AiQuestionService` da lay visible folder tu SQLite truoc khi query vector store.
  - Backend van re-validate chunk bang SQLite truoc khi dua vao answer.
- Phase 3 `Smart ingestion`: Done.
  - Da them normalize text va luu `normalized.txt`.
  - Da them section detection cho Markdown/numbered/Vietnamese headings.
  - Da chunk theo section va day metadata `section_title`, `section_index`, `char_start`, `char_end` vao Chroma.
  - Da luu `document_summary`, `section_count`, `processing_warnings_json` vao SQLite.
  - Citation/feedback/document detail da hien section va ingestion metadata.
  - Document understanding bang LLM structured output de tiep tuc o phase sau.
- Phase 4 `Better answer generation`: Done.
  - Da them `AiAnswerDraft` co structured fields: confidence, missing information, conflicts, suggested follow-ups, cited source ids.
  - OpenAI-compatible answer prompt yeu cau JSON schema va retry mot lan khi schema invalid.
  - Backend validate citation source id, chi tra citation nam trong context da duoc phep.
  - Da luu structured answer metadata vao `ai_interactions`.
  - UI Q&A hien confidence, missing information, conflicts va suggested follow-ups.
- Phase 5 `Wiki intelligence`: Done.
  - Wiki draft generation dung structured JSON output voi retry/fallback.
  - Wiki draft luu missing information va related documents dang JSON.
  - Backend tim related documents bang vector search co permission filter.
  - UI wiki draft hien related documents va missing information cho reviewer.
  - Khi publish, wiki chunks co them related/missing metadata count.
- Phase 6 `Feedback improvement loop`: Done.
  - Feedback Incorrect tao `AI Quality Issue` va job `ClassifyAiFailure`.
  - Worker phan loai failure rule-based va de xuat action.
  - Them `knowledge_corrections` va `retrieval_hints`.
  - Reviewer tao correction draft, approve correction va index vao vector store.
  - Retrieval include `correction` source type va uu tien correction truoc wiki/document.
  - UI Feedback Review hien quality issue va correction actions.
- Phase 7 `Evaluation`: Done.
  - Da them eval cases/runs/results de do before/after.
  - Reviewer tao eval case truc tiep tu Incorrect feedback.
  - Backend co API chay eval all active cases hoac mot case rieng.
  - Eval run goi lai `AiQuestionService`, luu answer that, pass/fail, score va failure reason.
  - Dashboard hien eval case active va pass rate cua run moi nhat.
- Phase 8 `Hybrid retrieval va rule reranking`: Done.
  - Da them query understanding rule-based de rut keyword tieng Viet/English tu cau hoi.
  - Tang candidate vector search len 50 truoc khi rerank.
  - Da them rule reranker boost correction/wiki/document, exact keyword, phrase match, scope match va distance.
  - Da them context packing toi da 8 chunks, moi knowledge item toi da 3 chunks, va bo trung chunk.
  - Test da chung minh exact keyword match co the vuot chunk gan vector hon, va context khong vuot gioi han packing.
- Phase 9 `SQLite keyword retrieval fallback`: Done.
  - Da them bang `knowledge_chunk_indexes` de mirror chunk text/metadata vao SQLite.
  - Document indexing, wiki publish va correction approve deu ghi keyword index song song voi Chroma.
  - Q&A merge vector candidates voi top keyword candidates truoc khi revalidate SQLite va rerank.
  - Keyword search van dung cung permission/scope/status filter nhu vector search.
  - Test da chung minh Q&A van lay duoc source khi vector search khong tra ket qua nhung keyword index match.
- Phase 10 `Document understanding metadata`: Done.
  - Da them `IDocumentUnderstandingService` voi mock heuristic va OpenAI-compatible structured JSON.
  - Ingestion sinh/lua metadata language, document type, summary, key topics, entities, effective date, sensitivity, quality warnings.
  - Metadata duoc luu vao SQLite `document_versions`, dua vao Chroma metadata va keyword index.
  - UI tai lieu hien language/type/sensitivity, topics, entities va quality warnings.
  - Test da chung minh mock understanding extract metadata va warning cho extraction yeu.
- Phase 11 `Retrieval explain`: Done.
  - Da them API `/api/ai/retrieval/explain` cho Admin/Reviewer.
  - Explain chay cung pipeline retrieval voi Q&A nhung khong generate answer va khong luu interaction.
  - Response tra query understanding, permission filter, candidate stats, candidate decision va final context.
  - UI reviewer co trang Retrieval de xem vector/keyword candidates, score, matched keywords va ly do bi loai/chon.
  - Test da chung minh explain tra diagnostics va khong tao `ai_interactions`.
- Phase 12 `Knowledge chunk ledger`: Done.
  - Da them bang SQLite `knowledge_chunks` de luu ledger chunk day du cho debug/rebuild vector DB.
  - Document indexing, wiki publish va correction approve deu ghi ledger song song voi Chroma va keyword index.
  - Ledger luu source metadata, text, text hash, vector id, chunk/section index va metadata JSON.
  - Test da chung minh ledger replace stale chunks, luu metadata va duoc ghi khi publish wiki/approve correction.
- Phase 13 `Knowledge index rebuild`: Done.
  - Da them rebuild service doc `knowledge_chunks`, tao lai embeddings va upsert lai vector store theo batch.
  - Co tuy chon reset Chroma collection truoc khi replay ledger.
  - Rebuild cung replace lai SQLite keyword index theo tung source tu ledger.
  - API/UI Admin/Reviewer co summary ledger/keyword counts va nut rebuild index.
  - Test da chung minh rebuild replay chunk, reset vector store va cap nhat keyword index.
- Cac hang muc sau Phase 13: Pending.

## 1. Muc tieu

Muc tieu san pham:

- Tra loi cau hoi noi bo dua tren tai lieu da duyet, wiki da publish va quyen truy cap cua user.
- Cau tra loi phai co nguon, do tin cay, va noi ro khi thieu thong tin.
- He thong khong chi "hoi dap tren chunk", ma co kha nang xu ly tai lieu thong minh: tom tat, phan loai, tim tai lieu lien quan, phat hien xung dot va sinh wiki co cau truc.
- Khi user bao cau tra loi sai, he thong phai ghi nhan, phan loai loi, de xuat hanh dong sua va dung ket qua sua de lan sau tra loi tot hon.
- LLM provider phai cau hinh duoc qua appsettings/environment variables, khong hard-code mot nha cung cap.

Muc tieu demo:

- Chung minh AI hieu tai lieu theo ngu nghia, khong chi search keyword.
- Chung minh AI khong tra loi vuot quyen.
- Chung minh AI biet khi nao thieu nguon hoac cau hoi mo ho.
- Chung minh feedback sai tao ra mot vong cai thien ro rang.
- Chung minh reviewer co quyen kiem soat tri thuc truoc khi he thong "hoc".

## 2. Hien trang can nang cap

He thong hien tai da co nen tang tot:

- Upload document, review, versioning.
- Extract text tu PDF/DOCX/Markdown/TXT.
- Chunk text.
- Upsert chunk vao ChromaDB.
- SQLite la source of truth cho user, folder, permission, document, wiki, feedback.
- AI Q&A co citation va co filter lai theo SQLite sau khi retrieve.
- Wiki draft/publish flow da co.

Diem yeu hien tai:

- `MockEmbeddingService` chi hash token thanh vector 64 chieu, khong phai semantic embedding that.
- `MockAnswerGenerationService` chua goi LLM that.
- `MockWikiDraftGenerationService` chi lay 900 ky tu dau roi nhet vao template.
- Chroma query lay top chunks toan collection truoc, sau do moi filter quyen, nen co the miss chunk dung neu top 30 deu la chunk user khong co quyen.
- Chunking dang dua chu yeu theo so ky tu, chua hieu heading/section/page.
- Feedback sai chua tu dong phan loai nguyen nhan va chua tao action de cai thien.
- Chua co evaluation set de do chat luong qua thoi gian.

## 3. Kien truc muc tieu

```text
[Vue UI]
   |
   v
[ASP.NET Core API]
   |
   +--> [SQLite]
   |      - users / roles / folders / permissions
   |      - documents / versions / extracted text metadata
   |      - wiki drafts / wiki pages
   |      - AI interactions / sources / feedback
   |      - AI failure classifications
   |      - knowledge corrections
   |      - evaluation cases / eval runs
   |
   +--> [Local File Storage]
   |      - original files
   |      - extracted.txt
   |      - optional normalized.txt
   |
   +--> [Smart Document Processing]
   |      - extract text
   |      - clean text / fix encoding
   |      - section detection
   |      - document summary
   |      - entity/keyword extraction
   |      - section-aware chunking
   |
   +--> [Vector Store: ChromaDB now, Qdrant later]
   |      - document chunks
   |      - wiki chunks
   |      - correction chunks
   |      - metadata filters for folder/document/status/source
   |
   +--> [LLM Provider Adapter]
          - embeddings
          - answer generation
          - wiki draft generation
          - feedback/failure classification
          - optional reranking
```

Nguyen tac quan trong:

- SQLite van la source of truth cho permission.
- Vector DB chi la index retrieval, khong phai noi quyet dinh quyen cuoi cung.
- Moi chunk lay tu vector DB phai duoc verify lai voi SQLite truoc khi dua vao prompt.
- He thong co the goi AI de de xuat, nhung moi thay doi tri thuc co tac dong toi cau tra loi ve sau phai co reviewer/admin approve.

## 4. LLM provider phai cau hinh duoc

Tao provider abstraction ro rang:

```text
ILLMChatService
IEmbeddingService
IRerankingService
IWikiDraftGenerationService
IFeedbackEvaluationService
IDocumentUnderstandingService
```

Config de xuat:

```text
AiProvider__Name=openai-compatible
AiProvider__BaseUrl=https://api.openai.com/v1
AiProvider__ApiKey=...
AiProvider__ChatEndpointMode=responses
AiProvider__ChatModel=gpt-5.5
AiProvider__FastModel=gpt-5.5
AiProvider__EmbeddingModel=text-embedding-3-large
AiProvider__EmbeddingDimension=3072
AiProvider__ReasoningEffort=medium
AiProvider__Temperature=0.2
AiProvider__MaxOutputTokens=2500
AiProvider__TimeoutSeconds=60
AiProvider__EnableStructuredOutputs=true
AiProvider__EnablePromptCaching=true
```

De ho tro nhieu nha cung cap:

```text
AiProvider__Name=mock | openai | openai-compatible | azure-openai | local
```

Mapping:

- `mock`: giu de test offline.
- `openai`: OpenAI official API.
- `openai-compatible`: bat ky API tuong thich OpenAI Chat Completions/Responses.
- `azure-openai`: endpoint/deployment rieng cua Azure.
- `local`: Ollama/vLLM/LM Studio neu can chay noi bo.

Khuyen nghi ky thuat:

- Dung model hoi-dap chat luong cao cho answer, wiki va failure classification.
- Dung model nho/nhanh cho tac vu nhe nhu language detection, classification don gian neu chi phi la van de.
- Dung embedding model da duoc huan luyen semantic that. Neu dung OpenAI, `text-embedding-3-large` phu hop khi can do chinh xac cao, `text-embedding-3-small` phu hop khi can tiet kiem hon.
- Khi doi embedding dimension, phai tao collection moi, vi vector DB collection thuong khong the tron vector 64/1536/3072 chieu trong cung collection.

## 5. Smart ingestion: xu ly tai lieu thong minh hon

Luon xu ly sau khi document version duoc approve.

Luong de xuat:

```text
Approved document version
-> Extract raw text
-> Normalize text
-> Detect language
-> Detect document type
-> Extract section outline
-> Extract keywords/entities
-> Generate document summary
-> Chunk by section/page/semantic boundary
-> Create embeddings
-> Upsert to vector store
-> Save processing report
```

### 5.1 Normalize text

Them `IDocumentTextNormalizer`:

- Chuan hoa line ending.
- Loai bo khoang trang thua.
- Gop tu bi tach sai do PDF.
- Phat hien text bi mojibake/encoding loi.
- Luu `normalized.txt` ben canh `extracted.txt`.

Acceptance:

- Text tieng Viet hien thi dung dau trong `normalized.txt`.
- Neu text extract qua xau, version co `ProcessingWarning` de reviewer biet.

### 5.2 Section detection

Them `ISectionDetector`:

- Markdown: heading `#`, `##`.
- DOCX: heading styles neu co.
- PDF/TXT: detect bang pattern `I.`, `1.`, `1.1`, chu in hoa, dong ngan.

Tao model noi bo:

```csharp
public sealed record DocumentSection(
    string SectionId,
    string Title,
    int Order,
    int? PageStart,
    int? PageEnd,
    string Text);
```

### 5.3 Metadata giau hon

Chunk metadata nen them:

```text
source_type=document
source_id={document_version_id}
document_id
document_version_id
folder_id
folder_path
title
version_number
status=approved
visibility_scope=folder
section_id
section_title
chunk_index
page_start
page_end
language
document_type
keywords
entities
text_hash
indexed_at
```

Metadata nay giup:

- Filter theo quyen.
- Hien citation co section/page.
- Rerank tot hon.
- Tim tai lieu lien quan theo document_type/keywords/entities.

### 5.4 Document understanding bang LLM

Sau khi normalize, goi LLM de tao `DocumentUnderstandingResult` theo JSON schema:

```json
{
  "language": "vi",
  "documentType": "pricing|policy|procedure|technical|contract|faq|unknown",
  "summary": "...",
  "keyTopics": ["..."],
  "entities": ["..."],
  "effectiveDate": null,
  "sensitivity": "normal|internal|confidential",
  "qualityWarnings": ["..."]
}
```

Luu vao SQLite, khong chi luu trong Chroma.

## 6. Permission-aware retrieval

Hien tai:

```text
query Chroma top 30 all collection
-> filter permission sau
```

Nen doi thanh:

```text
Get visibleFolderIds from SQLite
-> Build vector metadata filter
-> Query Chroma with folder/document/wiki filters
-> Re-validate result against SQLite
```

Filter de xuat:

Scope `All`:

```text
source_type in [wiki, document, correction]
AND (
  visibility_scope = company
  OR folder_id in visibleFolderIds
)
AND status in [published, approved]
```

Scope `Folder`:

```text
folder_id = selectedFolderId
AND selectedFolderId in visibleFolderIds
```

Scope `Document`:

```text
document_id = selectedDocumentId
AND document folder in visibleFolderIds
```

Sau retrieve van check lai:

- Document chunk: version phai `Indexed`, document chua bi xoa, version phai la `CurrentVersionId`.
- Wiki chunk: page chua archived, visibility con hop le.
- Correction chunk: correction phai `Approved`, scope hop le.

## 7. Hybrid retrieval va reranking

Vector search tot cho ngu nghia, nhung tai lieu noi bo can keyword exact.

Pipeline de xuat:

```text
Question
-> Query understanding
-> Dense vector search
-> Keyword/full-text search
-> Optional exact metadata search
-> Merge candidates
-> Rerank
-> Context packing
```

### 7.1 Query understanding

Dung LLM hoac rule nhe de tao:

```json
{
  "rewrittenQuestion": "...",
  "keywords": ["VNPT", "token", "hoa don dien tu"],
  "entities": ["VNPT", "Misa"],
  "documentTypeHint": "technical",
  "needsClarification": false,
  "clarifyingQuestion": null
}
```

Neu cau hoi qua mo ho, hoi lai truoc khi search sau:

```text
"Ban muon hoi ve nha cung cap VNPT hay toan bo quy trinh hoa don dien tu?"
```

### 7.2 Candidate retrieval

Lay nhieu hon mot nguon:

- Wiki published: top 20.
- Document chunks: top 40.
- Knowledge corrections: top 10.
- Keyword search: top 20.

Sau do merge theo chunk id/document id.

### 7.3 Reranking

Co 3 muc:

1. Rule reranker: boost wiki, current version, exact keyword match, same folder/document scope.
2. LLM reranker: model cham diem relevance 0-5 cho tung chunk.
3. Dedicated reranker model neu sau nay can toi uu chi phi/chat luong.

Voi MVP nang cap, co the lam rule reranker truoc, LLM reranker sau.

### 7.4 Context packing

Khong dua chunk trung lap vao LLM.

Quy tac:

- Toi da 8 chunks.
- Moi document toi da 3 chunks.
- Uu tien correction/wiki/document theo thu tu:
  1. Approved correction.
  2. Published wiki.
  3. Current indexed document.
- Neu co xung dot, dua ca hai nguon lien quan de LLM noi ro xung dot.

## 8. Answer generation thong minh

Dung structured output thay vi text tu do.

Response schema de xuat:

```json
{
  "answer": "...",
  "confidence": "high|medium|low",
  "needsClarification": false,
  "clarifyingQuestion": null,
  "citations": [
    {
      "sourceType": "Wiki",
      "sourceId": "...",
      "title": "...",
      "folderPath": "...",
      "sectionTitle": "...",
      "pageStart": 1,
      "excerpt": "..."
    }
  ],
  "missingInformation": [],
  "conflicts": [],
  "suggestedFollowUps": []
}
```

Prompt contract:

- Chi tra loi dua tren context.
- Khong doan neu khong co nguon.
- Neu thieu thong tin, noi ro thieu gi.
- Neu context xung dot, noi ro nguon nao xung dot.
- Neu user khong co quyen voi tai lieu, khong nhac ten tai lieu do.
- Tra loi bang tieng Viet.
- Citation phai lay tu chunk da duoc backend cung cap.

Backend phai validate:

- Citation source id co nam trong context khong.
- Neu answer co claim nhung khong co citation, ha confidence hoac reject/retry.
- Neu LLM tra JSON loi schema, retry mot lan voi prompt sua loi.

## 9. Wiki generation thong minh

Wiki draft hien tai lay 900 ky tu dau. Nen doi thanh:

```text
Document current indexed version
-> Read normalized text + sections + summary
-> Find related documents/wiki/corrections
-> Generate structured wiki draft
-> Save as draft for reviewer
```

Wiki draft schema:

```json
{
  "title": "...",
  "language": "vi",
  "purpose": "...",
  "scope": "...",
  "audience": ["..."],
  "mainContent": [
    {
      "heading": "...",
      "content": "...",
      "citations": ["chunk-id-1"]
    }
  ],
  "procedure": ["..."],
  "risksAndNotes": ["..."],
  "faq": [
    {
      "question": "...",
      "answer": "...",
      "citations": ["chunk-id-2"]
    }
  ],
  "relatedDocuments": [
    {
      "documentId": "...",
      "title": "...",
      "reason": "Co cung chu de ket noi hoa don dien tu VNPT"
    }
  ],
  "missingInformation": ["..."]
}
```

Tim tai lieu lien quan:

```text
Use document summary/key topics
-> Query vector store with permission filter
-> Group by document_id
-> Remove current document
-> Rank by overlap/relevance
-> Show top 5 related documents in draft
```

Gia tri demo:

- Reviewer thay AI khong chi tom tat mot file, ma con phat hien tai lieu lien quan trong kho tri thuc.
- Khi publish wiki, wiki tro thanh nguon uu tien cho Q&A.

## 10. He thong tien bo khi gap loi

Khong nen de AI tu sua tri thuc ma khong co reviewer. Nen lam "continuous improvement with human approval".

### 10.1 Feedback loop

```text
User asks question
-> System answers with citations
-> User marks Incorrect + note
-> Store feedback
-> LLM classifies failure
-> Create AI Quality Issue
-> Reviewer chooses corrective action
-> System applies approved correction/reindex/regenerate
-> Add case to eval set
-> Future answers use improved knowledge/retrieval
```

### 10.2 Failure classification

Dung LLM de phan loai feedback sai:

```json
{
  "failureType": "NoRelevantContext|WrongContext|BadAnswer|MissingDocument|BadExtraction|BadChunking|OutdatedContext|PermissionIssue|AmbiguousQuestion|ConflictingSources",
  "severity": "low|medium|high",
  "rootCauseHypothesis": "...",
  "recommendedActions": [
    "ReindexDocument",
    "CreateCorrection",
    "RegenerateWiki",
    "AskClarification",
    "UploadMissingDocument"
  ],
  "evidence": {
    "usedSources": ["..."],
    "userNote": "...",
    "missingKeywords": ["..."]
  }
}
```

### 10.3 AI Quality Issue

Them bang:

```text
ai_quality_issues
  id
  feedback_id
  interaction_id
  failure_type
  severity
  root_cause_hypothesis
  recommended_actions_json
  status: Open | InReview | Fixed | Rejected | Won'tFix
  assigned_to_user_id
  created_at
  resolved_at
```

UI reviewer:

- Xem cau hoi.
- Xem answer sai.
- Xem citations da dung.
- Xem note user.
- Xem AI classification.
- Chon hanh dong sua.

### 10.4 Corrective actions

Hanh dong nen co:

1. `CreateCorrection`
   - Reviewer viet/sua cau tra loi dung.
   - Luu thanh `knowledge_corrections`.
   - Correction duoc chunk/index vao vector DB voi `source_type=correction`.
   - Lan sau correction duoc uu tien hon wiki/document.

2. `ReindexDocument`
   - Re-run extraction/normalization/chunking/embedding cho document version.
   - Dung khi loi do extraction/chunking/index.

3. `RegenerateWiki`
   - Sinh lai wiki draft tu document + feedback.
   - Reviewer approve/publish.

4. `AddRetrievalHint`
   - Luu synonyms/keywords cho query rewriting.
   - Vi du: "HDDT" = "hoa don dien tu".

5. `MarkMissingDocument`
   - Tao task yeu cau upload tai lieu thieu.

6. `PromptTuningProposal`
   - Luu de xuat sua prompt, chi admin apply.

### 10.5 Knowledge corrections

Bang de xuat:

```text
knowledge_corrections
  id
  title
  question_pattern
  corrected_answer
  folder_id nullable
  document_id nullable
  visibility_scope
  status: Draft | Approved | Archived
  created_from_feedback_id
  approved_by_user_id
  approved_at
```

Khi Approved:

```text
chunk corrected_answer
-> embedding
-> upsert source_type=correction
```

Retrieval uu tien:

```text
correction > wiki > document
```

Day la cach he thong "tien bo" ma van an toan, co audit va reviewer approval.

## 11. Evaluation va do tien bo

Moi feedback sai nen co the bien thanh eval case.

Bang:

```text
ai_evaluation_cases
  id
  question
  expected_answer
  expected_source_ids_json
  scope_type
  folder_id
  document_id
  created_from_feedback_id
  status
```

Chay eval:

```text
dotnet test or admin endpoint
-> run selected eval cases
-> compare answer/citations with expected
-> LLM judge optional
-> save ai_evaluation_runs
```

Chi so:

- Citation hit rate.
- Answer correctness rate.
- No-answer accuracy: khi thieu nguon phai noi thieu, khong bia.
- Permission safety: khong dung chunk trai quyen.
- Feedback incorrect rate.
- Reopened issue rate.

Demo "he thong tien bo":

1. Hoi cau A, AI tra loi sai.
2. User feedback sai.
3. Reviewer thay AI quality issue, approve correction.
4. Hoi lai cau A, AI dung correction lam nguon uu tien.
5. Dashboard hien eval case pass sau khi fixed.

## 12. Data model thay doi

Them/thay doi bang:

```text
document_processing_reports
  id
  document_version_id
  language
  document_type
  summary
  key_topics_json
  entities_json
  quality_warnings_json
  normalized_text_path
  created_at

document_sections
  id
  document_version_id
  title
  order
  page_start
  page_end
  text_hash

knowledge_chunks
  id
  source_type
  source_id
  document_id
  document_version_id
  wiki_page_id
  correction_id
  folder_id
  section_id
  title
  section_title
  chunk_index
  text
  text_hash
  vector_id
  created_at

ai_quality_issues
  id
  feedback_id
  interaction_id
  failure_type
  severity
  root_cause_hypothesis
  recommended_actions_json
  status
  assigned_to_user_id
  created_at
  resolved_at

knowledge_corrections
  id
  title
  question_pattern
  corrected_answer
  visibility_scope
  folder_id
  document_id
  status
  created_from_feedback_id
  approved_by_user_id
  approved_at
  created_at
  updated_at

retrieval_hints
  id
  phrase
  synonyms_json
  boost_keywords_json
  folder_id
  status
  created_from_feedback_id

ai_evaluation_cases
  id
  question
  expected_answer
  expected_source_ids_json
  scope_type
  folder_id
  document_id
  created_from_feedback_id
  status

ai_evaluation_runs
  id
  case_id
  answer
  citations_json
  score
  passed
  details_json
  created_at
```

Luu y:

- `knowledge_chunks` trong SQLite giup debug va rebuild vector DB de hon.
- Vector DB van giu embeddings va metadata; SQLite giu source truth va audit.

## 13. API thay doi

### AI ask

`POST /api/ai/ask`

Them response fields:

```json
{
  "interactionId": "...",
  "answer": "...",
  "confidence": "high",
  "needsClarification": false,
  "clarifyingQuestion": null,
  "citations": [],
  "missingInformation": [],
  "conflicts": [],
  "suggestedFollowUps": []
}
```

### Feedback

`POST /api/ai/interactions/{id}/feedback`

Sau khi feedback sai:

- Tao feedback.
- Queue job `ClassifyAiFailure`.
- Tao `ai_quality_issue`.

### Quality review

Endpoints moi:

```text
GET  /api/ai/quality/issues
GET  /api/ai/quality/issues/{id}
POST /api/ai/quality/issues/{id}/classify
POST /api/ai/quality/issues/{id}/corrections
POST /api/ai/quality/issues/{id}/reindex-document
POST /api/ai/quality/issues/{id}/resolve
```

### Debug/retrieval explain

Chi Admin/Reviewer:

```text
POST /api/ai/retrieval/explain
```

Tra ve:

- Query rewrite.
- Visible folder count.
- Vector filter.
- Candidate chunks before rerank.
- Final context chunks.
- Chunks bi loai va ly do.

Day la man hinh demo rat tot de chung minh he thong co logic thong minh, khong phai black box.

## 14. UI thay doi

### 14.1 AI answer UI

Them:

- Confidence badge.
- Citations co section/page.
- "Thieu thong tin" list.
- "Nguon xung dot" list.
- Suggested follow-up questions.
- Nut "Giai thich vi sao chon nguon nay" cho Reviewer/Admin.

### 14.2 AI Quality Review UI

Trang moi:

```text
/review/ai-quality
```

Cot danh sach:

- Time.
- User.
- Question.
- Failure type.
- Severity.
- Status.

Detail:

- Question.
- Answer.
- Citations da dung.
- User feedback note.
- AI root cause hypothesis.
- Recommended actions.
- Buttons:
  - Create correction
  - Reindex document
  - Regenerate wiki
  - Add retrieval hint
  - Mark missing document
  - Resolve

### 14.3 Document processing report UI

Trong document detail:

- Language.
- Document type.
- Summary.
- Quality warnings.
- Sections detected.
- Chunk count.
- Last indexed at.

## 15. Demo kich ban chung minh thong minh

### Demo 1: Semantic question, co citation

Data:

- Upload va approve tai lieu "Bao gia dich vu ket noi hoa don dien tu".

Hoi:

```text
Muon ket noi hoa don dien tu voi VNPT thi he thong can xu ly nhung buoc nao?
```

Ky vong:

- AI tra loi theo nghia, khong can dung dung keyword.
- Co citation den section lien quan.
- Neu wiki da publish, AI uu tien wiki.

### Demo 2: Permission safety

Data:

- User A chi co quyen folder "Ho tro khach hang".
- Tai lieu "Bao gia HDDT" nam trong folder "Ky thuat".

Hoi bang User A:

```text
Bao gia ket noi hoa don dien tu VNPT la bao nhieu?
```

Ky vong:

- Retrieval filter khong lay chunk folder Ky thuat.
- AI noi khong co nguon phu hop trong pham vi duoc phep.
- Khong lo ten tai lieu trai quyen.

### Demo 3: Ambiguous question

Hoi:

```text
Quy trinh nay can lam gi tiep?
```

Ky vong:

- AI khong doan.
- Tra clarifying question:
  "Ban dang hoi quy trinh nao hoac tai lieu nao?"

### Demo 4: Related documents khi sinh wiki

Data:

- Document A: Bao gia HDDT.
- Document B: Quy trinh ket noi VNPT.
- Document C: Cau hinh API Misa.

Generate wiki tu Document A.

Ky vong:

- Draft wiki co phan "Tai lieu lien quan".
- Giai thich Document B lien quan vi cung noi ve ket noi VNPT/hoa don dien tu.
- Reviewer thay AI co kha nang lien ket tri thuc.

### Demo 5: He thong tien bo sau loi

Buoc 1: Hoi:

```text
Khi nha cung cap tra ve loi hoa don thi can lam gi?
```

Gia su AI tra loi chua dung/khong du.

Buoc 2: User bam "Sai" va ghi:

```text
Thieu buoc ghi log va retry theo ma loi.
```

Buoc 3: Reviewer vao AI Quality Review.

He thong classify:

```text
Failure type: MissingInformation
Recommended action: CreateCorrection + RegenerateWiki
```

Buoc 4: Reviewer approve correction:

```text
Khi nha cung cap tra ve loi, he thong phai ghi log chi tiet, phan loai ma loi, retry neu loi tam thoi, va thong bao reviewer neu loi nghiep vu.
```

Buoc 5: Hoi lai cung cau hoi.

Ky vong:

- AI dung correction lam nguon uu tien.
- Cau tra loi co them buoc ghi log/retry.
- Citation hien source type `Correction`.
- Dashboard/eval case chuyen sang pass.

Day la demo tot nhat de chung minh "he thong tien bo".

## 16. Lo trinh trien khai

### Phase 1: Provider va embedding that - Done 2026-05-12

Muc tieu:

- Thay mock embedding va mock answer bang provider cau hinh duoc.

Cong viec:

- Them `AiProviderOptions`.
- Them `OpenAiCompatibleChatService`.
- Them `OpenAiCompatibleEmbeddingService`.
- Giu `Mock*` cho test.
- Them collection moi `knowledge_chunks_v2`.
- Reindex document hien co.
- Update `.env.example`.

Acceptance:

- Co the doi model/base URL/api key bang config.
- Document approved tao embedding semantic that.
- AI answer goi LLM that va co citation.

### Phase 2: Permission-aware retrieval - Done 2026-05-12

Muc tieu:

- Query vector store voi metadata filter theo quyen/scope truoc.

Cong viec:

- Mo rong `IKnowledgeVectorStore.QueryAsync` nhan `KnowledgeQueryFilter`.
- Chroma implementation build `where`.
- AI service lay visible folders truoc query.
- Van revalidate bang SQLite sau query.
- Them tests cho user khong co quyen.

Acceptance:

- Khong retrieve chunk trai quyen.
- Neu top chunks trai quyen, user van co the tim thay chunk dung trong folder duoc phep.

### Phase 3: Smart ingestion - Done 2026-05-12

Muc tieu:

- Normalize text, detect section, document summary, richer metadata.

Cong viec:

- Them `DocumentTextNormalizer`.
- Them `SectionDetector`.
- Them processing report tables.
- Them `DocumentUnderstandingService` dung LLM structured output.
- Chunk theo section.
- Luu chunk metadata vao SQLite va Chroma.

Da trien khai:

- `DocumentTextNormalizer` normalize newline/whitespace, Unicode FormC va warning encoding issue.
- `SectionDetector` detect Markdown heading, numbered heading va cac heading tieng Viet pho bien.
- `TextChunker` chunk theo section va giu section metadata.
- Document indexing ghi `normalized.txt`, summary heuristic, section count va warnings.
- Document/wiki chunks ghi `section_title`, `section_index`, `char_start`, `char_end` vao vector metadata.
- AI citations va feedback sources tra ve `sectionTitle`.
- Document detail tra ve `hasNormalizedText`, `sectionCount`, `documentSummary`, `processingWarnings`.

Chua lam trong Phase 3 core:

- `DocumentUnderstandingService` bang LLM structured output; nen lam sau khi Phase 4 co structured output/retry/validation chung.

Acceptance:

- Document detail hien summary/sections/warnings.
- Citation co section title.
- Text tieng Viet khong bi loi encoding trong normalized output.

### Phase 4: Better answer generation - Done 2026-05-13

Muc tieu:

- Answer co JSON schema, confidence, missing info, conflicts.

Cong viec:

- Dinh nghia `GroundedAnswerDraft`.
- Update prompt answer.
- Validate citations.
- Add retry khi schema invalid.
- Update UI answer.

Acceptance:

- AI khong bia khi khong co context.
- Answer co confidence.
- Citation source id hop le.

Da trien khai:

- `AiAnswerDraft` gom `Answer`, `NeedsClarification`, `Confidence`, `MissingInformation`, `Conflicts`, `SuggestedFollowUps`, `CitedSourceIds`.
- `OpenAiCompatibleAnswerGenerationService` yeu cau provider tra JSON only theo schema.
- Neu provider tra schema sai, backend retry mot lan voi repair prompt.
- Neu van sai schema, backend fallback confidence `low`, `needsClarification=true` va ghi missing info.
- Backend map citation label `S1`, `S2` ve `SourceId` that cua chunk; citation ngoai context bi loai.
- `AiQuestionService` chi tra ve va chi luu citations da duoc answer draft cite hop le.
- `ai_interactions` luu `confidence`, `missing_information_json`, `conflicts_json`, `suggested_follow_ups_json`.
- UI Q&A hien confidence, missing information, conflicts va suggested follow-ups.

### Phase 5: Wiki intelligence - Done 2026-05-13

Muc tieu:

- Wiki draft co cau truc that, co related documents, co missing info.

Cong viec:

- Dung LLM structured output cho wiki.
- Tim related docs qua vector search + permission filter.
- Them UI hien related documents.
- Khi publish, index wiki voi metadata giau hon.

Acceptance:

- Draft khong con copy 900 ky tu dau.
- Reviewer thay duoc ly do tai lieu lien quan.

Da trien khai:

- `IWikiDraftGenerationService` tra `WikiDraftContent` gom markdown content, language va missing information.
- `OpenAiCompatibleWikiDraftGenerationService` yeu cau provider tra structured JSON, parse sang Markdown va retry mot lan khi schema invalid.
- `MockWikiDraftGenerationService` tao draft theo sections heuristic thay vi copy 900 ky tu dau.
- `WikiService.GenerateDraftAsync` uu tien `normalized.txt`, sinh related documents bang vector search trong visible folders, loai current document.
- `wiki_drafts` luu `missing_information_json` va `related_documents_json`.
- API `WikiDraftDetailResponse` tra `missingInformation` va `relatedDocuments`.
- UI Wiki drafts hien related documents kem reason va missing information.
- Publish wiki index them `related_document_count` va `missing_information_count` vao vector metadata.

### Phase 6: Feedback improvement loop - Done 2026-05-13

Muc tieu:

- Feedback sai tao quality issue, phan loai loi va hanh dong sua.

Cong viec:

- Them tables `ai_quality_issues`, `knowledge_corrections`, `retrieval_hints`.
- Them job `ClassifyAiFailure`.
- Them Quality Review UI.
- Implement CreateCorrection + index correction.
- Retrieval uu tien correction.

Acceptance:

- User feedback sai tao issue.
- Reviewer approve correction.
- Lan hoi sau dung correction.

Da trien khai:

- Them enum `AiQualityIssueStatus`, `KnowledgeCorrectionStatus`, source type `Correction`.
- Them tables `ai_quality_issues`, `knowledge_corrections`, `retrieval_hints`.
- Khi user submit feedback `Incorrect`, backend tao quality issue va pending job `ClassifyAiFailure`.
- `ProcessingJobWorker` xu ly job `ClassifyAiFailure`, phan loai failure rule-based, luu severity/root cause/recommended actions/evidence.
- Reviewer co API/UI xem quality issues, tao correction draft, approve/reject correction.
- Approve correction se index correction vao vector store voi metadata `source_type=correction`, `status=approved`, permission scope va audit log.
- `AiQuestionService` query include correction, revalidate correction bang SQLite, va uu tien correction truoc wiki/document.
- Test da chung minh answer lan sau uu tien approved correction chunk.

### Phase 7: Evaluation

Muc tieu:

- Do duoc he thong co tien bo hay khong.

Cong viec:

- Them eval cases/runs.
- Cho reviewer tao eval case tu feedback.
- Them command/API chay eval.
- Dashboard KPI chat luong.

Acceptance:

- Co baseline before/after.
- Demo duoc mot case fail -> fix -> pass.

Da trien khai:

- Them tables `evaluation_cases`, `evaluation_runs`, `evaluation_run_results`.
- Them `IEvaluationService` va API `/api/evaluation/cases`, `/api/evaluation/feedback/{feedbackId}/cases`, `/api/evaluation/runs`.
- Reviewer tao eval case tu feedback sai voi expected answer va expected keywords.
- Eval run chay lai cau hoi qua pipeline AI hien tai, luu `AiInteractionId`, actual answer, score, pass/fail va ly do fail.
- Dashboard summary co KPI `evaluationCaseCount`, latest eval pass count/pass rate/run time.
- UI Feedback co form tao eval case; UI Evaluation co nut chay all active cases hoac tung case.
- Test unit chung minh case pass khi answer co expected keywords va fail khi thieu keyword.

### Phase 8: Hybrid retrieval va rule reranking

Muc tieu:

- Cai thien chat luong context truoc khi dua vao answer generation.
- Giam truong hop vector distance gan hon nhung thieu keyword/entity quan trong.

Cong viec:

- Them query understanding rule-based.
- Rerank candidate chunks bang source priority, exact keyword match, phrase match, scope match va distance.
- Context packing de tranh dua qua nhieu chunk trung lap tu cung mot tai lieu.

Acceptance:

- Chunk match keyword/entity quan trong duoc uu tien hon chunk vector gan nhung lech noi dung.
- Context dua vao LLM khong qua 8 chunks.
- Moi document/wiki/correction khong chiem qua 3 chunks trong mot prompt.

Da trien khai:

- `AiQuestionService` rut keyword sau khi normalize dau tieng Viet va bo stop words.
- Candidate search limit tang tu 30 len 50 de co them ung vien cho rerank.
- Rerank score gom source boost: correction > wiki > document, keyword/phrase boost, scope boost va distance score.
- Context packing bo trung source/section va gioi han toi da 8 chunks, toi da 3 chunks moi knowledge item.
- Test `AskAsync_ReranksExactKeywordMatchAheadOfCloserVectorDistance` chung minh exact keyword duoc uu tien.
- Test `AskAsync_PacksAtMostEightChunksAndThreePerDocument` chung minh context packing dung gioi han.

### Phase 9: SQLite keyword retrieval fallback

Muc tieu:

- Hoan thien pipeline hybrid retrieval bang keyword search noi bo.
- Giam truong hop vector search bo sot ten rieng, ma loi, keyword nghiep vu hoac cum tu chinh xac.

Cong viec:

- Mirror chunk da index vao SQLite de search keyword.
- Khi hoi dap, lay top keyword candidates va merge voi vector candidates.
- Van revalidate permission/status/current-version bang SQLite truoc khi dua vao answer.

Acceptance:

- Document/wiki/correction moi index co keyword row trong SQLite.
- Neu vector search khong tra ket qua, keyword search van co the dua chunk dung vao answer.
- Keyword candidates khong vuot quyen va khong dung source da archived/outdated/rejected.

Da trien khai:

- Them entity/table `KnowledgeChunkIndexEntity` / `knowledge_chunk_indexes`.
- Them `IKnowledgeKeywordIndexService` de replace chunks theo source va search top keyword candidates.
- `DocumentProcessingService`, `WikiService`, `AiFeedbackService` ghi keyword index khi index document/wiki/correction.
- `AiQuestionService` merge vector results va keyword results truoc `FilterAllowedChunksAsync`, nen van dung revalidation hien co.
- Test `AskAsync_UsesKeywordIndexWhenVectorSearchHasNoResults` chung minh fallback keyword hoat dong.
- Test wiki/correction dam bao publish/approve tao keyword index.

### Phase 10: Document understanding metadata

Muc tieu:

- Hoan thien phan con thieu cua Smart Ingestion: hieu tai lieu sau normalize, khong chi chunk text.
- Luu metadata co cau truc vao SQLite de dung cho dashboard/retrieval/reviewer ve sau.

Cong viec:

- Them `IDocumentUnderstandingService`.
- Provider `mock` dung heuristic offline; provider OpenAI-compatible dung structured JSON voi retry/fallback.
- Luu language, document type, summary, key topics, entities, effective date, sensitivity, quality warnings vao `document_versions`.
- Dua metadata vao Chroma chunk metadata va keyword index.
- Hien metadata trong document detail UI.

Acceptance:

- Tai lieu sau khi index co language/document type/topics/entities/sensitivity trong SQLite.
- Summary duoc lay tu understanding result neu co.
- Metadata duoc day vao retrieval index.
- Test offline khong can API key van chung minh extraction metadata.

Da trien khai:

- Them columns `language`, `document_type`, `key_topics_json`, `entities_json`, `effective_date`, `sensitivity`, `quality_warnings_json`.
- Them `MockDocumentUnderstandingService` va `OpenAiCompatibleDocumentUnderstandingService`.
- `DocumentProcessingService` goi understanding sau normalize/section detection va truoc chunk/index.
- Document chunk metadata co `language`, `document_type`, `keywords`, `entities`, `sensitivity`, `text_hash`.
- Keyword index search them `keywords` va `entities` vao normalized text.
- UI document version hien metadata understanding.
- Test `DocumentUnderstandingServiceTests` cover metadata va warning extraction.

### Phase 11: Retrieval explain

Muc tieu:

- Cho Admin/Reviewer nhin thay vi sao he thong chon hoac loai bo tung source trong retrieval.
- Chung minh permission filter, hybrid vector/keyword retrieval, rerank va context packing dang hoat dong ro rang.
- Debug duoc case cau tra loi yeu ma khong can doc log backend hay goi LLM.

Cong viec:

- Them `IAiQuestionService.ExplainRetrievalAsync`.
- Them endpoint `POST /api/ai/retrieval/explain` chi danh cho Admin/Reviewer.
- Reuse pipeline query understanding -> embedding -> vector search -> keyword search -> merge -> permission revalidation -> rerank/context packing.
- Response gom query keywords, filter da ap dung, candidate counts, final context va diagnostics cho tung candidate.
- Them UI `/retrieval-explain` de reviewer xem score, matched keywords, source vector/keyword, ly do reject/select.

Acceptance:

- Explain khong tao `ai_interactions` va khong goi answer generation.
- Candidate bi loai co decision ro: sai scope, folder khong visible, document khong current indexed, wiki/correction khong visible/approved.
- Final context hien score reasons de reviewer thay correction/wiki/document priority, keyword match, scope match va distance score.
- Frontend build duoc va reviewer/admin truy cap duoc route.

Da trien khai:

- `RetrievalExplainResponse` gom query understanding, filter, candidate stats, final context va candidates.
- `AiQuestionService` tach filter analysis co allowed/rejected diagnostics.
- Scoring explain tra matched keywords va score reasons.
- `AiController` them endpoint `retrieval/explain` voi role Admin/Reviewer.
- Frontend them `explainRetrieval`, page `RetrievalExplainPage.vue`, route va nav item `Retrieval`.
- Test `ExplainRetrievalAsync_ReturnsDiagnosticsWithoutSavingInteraction` cover diagnostics va khong luu interaction.

### Phase 12: Knowledge chunk ledger

Muc tieu:

- Luu day du chunk da index vao SQLite de co ledger debug/rebuild, khong chi phu thuoc vao Chroma va keyword index.
- Giu SQLite la source truth cho text/metadata chunk; vector DB chi giu embedding index.
- Tao nen tang cho phase sau: rebuild vector collection, compare drift giua SQLite va Chroma, audit chunk theo source.

Cong viec:

- Them entity/table `knowledge_chunks`.
- Them `IKnowledgeChunkLedgerService` voi `ReplaceChunksAsync` va `GetChunksForSourceAsync`.
- Tich hop ledger vao document indexing, wiki publish va correction approve.
- Them `chunk_index` vao metadata document/wiki/correction.
- Tao migration SQLite.
- Them unit test cho ledger va integration test voi wiki/correction.

Acceptance:

- Moi lan index lai cung source thi stale chunks trong ledger bi replace.
- Ledger luu du source type/source id/document/wiki/correction/folder/status/visibility/text/hash/vector id/metadata JSON.
- Wiki publish va correction approve tao dong trong `knowledge_chunks` song song voi `knowledge_chunk_indexes`.
- Test offline pass khong can Chroma/API key.

Da trien khai:

- Them `KnowledgeChunkEntity` mapped vao bang `knowledge_chunks`.
- Them migration `AddKnowledgeChunksLedger`.
- `KnowledgeChunkLedgerService` luu chunk text, metadata JSON va SHA-256 text hash; co snapshot API noi bo cho rebuild.
- `DocumentProcessingService`, `WikiService`, `AiFeedbackService` goi ledger khi upsert chunks.
- Test `KnowledgeChunkLedgerServiceTests` cover replace/stale chunk va metadata snapshot.
- Test wiki/correction verify ledger rows duoc tao khi publish/approve.

### Phase 13: Knowledge index rebuild

Muc tieu:

- Dung `knowledge_chunks` ledger lam source de rebuild lai vector/keyword index khi doi collection, doi embedding model hoac nghi ngo drift.
- Cho Admin/Reviewer chay rebuild co kiem soat, thay duoc so chunk se replay.
- Giam rui ro mat index Chroma vi SQLite da giu day du text/metadata chunk.

Cong viec:

- Mo rong `IKnowledgeVectorStore` voi `ResetCollectionAsync`.
- Chroma implementation co the delete/recreate collection hien tai.
- Them `IKnowledgeIndexRebuildService`.
- Service doc ledger, tao embedding moi tu chunk text, upsert lai vector store theo batch.
- Service replace lai keyword index theo tung source tu ledger.
- Them API `/api/knowledge-index/summary` va `/api/knowledge-index/rebuild` cho Admin/Reviewer.
- Them UI `/knowledge-index` de xem ledger/keyword counts va chay rebuild.

Acceptance:

- Rebuild khong can upload/index lai document goc.
- Reset vector store la tuy chon ro rang.
- Keyword index duoc tao lai tu ledger sau rebuild.
- Test offline chung minh replay chunk, reset vector va rebuild keyword index.

Da trien khai:

- `ChromaKnowledgeVectorStore.ResetCollectionAsync` reset collection hien tai.
- `KnowledgeIndexRebuildService` rebuild tu `knowledge_chunks`, batch size clamp 1..200, audit action `KnowledgeIndexRebuilt`.
- `KnowledgeIndexController` expose summary/rebuild endpoints.
- Frontend them `knowledgeIndex.ts`, page `KnowledgeIndexPage.vue`, route/nav `Index`.
- Test `KnowledgeIndexRebuildServiceTests` cover replay, reset va summary.

## 17. Thu tu uu tien neu thoi gian ngan

Neu can demo nhanh, lam theo thu tu:

1. Real LLM answer + real embeddings.
2. Permission-aware Chroma filter.
3. Structured answer with confidence/citations.
4. Correction source type + Quality Review toi gian.
5. Better wiki draft bang LLM.

Bo qua tam:

- Full evaluation dashboard.
- Dedicated reranker.
- OCR nang cao.
- Local LLM deployment.
- Fine-tuning.

## 18. Rui ro va cach giam

### Leak du lieu trai quyen

Giam bang:

- Filter Chroma theo visible folder.
- Revalidate SQLite truoc prompt.
- Khong dua chunk trai quyen vao prompt.
- Audit interaction sources.

### LLM hallucination

Giam bang:

- Grounded prompt.
- Structured output.
- Citation validation.
- "No sufficient source" behavior.
- Feedback/eval loop.

### Chi phi API tang

Giam bang:

- Cache embeddings theo text hash.
- Prompt caching cho system/developer prompts.
- Dung model nho cho classification.
- Batch embedding khi reindex.
- Gioi han max chunks/context.

### Latency cao

Giam bang:

- Query/rerank theo pipeline nhanh.
- Stream answer neu can.
- Reasoning effort configurable.
- Background process cho ingestion/wiki/classification.

### AI tu hoc sai

Giam bang:

- Khong auto-approve correction.
- Reviewer approve moi index correction/wiki.
- Audit moi hanh dong sua.

## 19. Cong nghe de xuat

Bat buoc/nen lam:

- ChromaDB hien tai tiep tuc dung duoc, nhung phai dung metadata filtering.
- LLM provider adapter theo OpenAI-compatible API.
- Structured outputs cho answer/wiki/failure classification.
- SQLite tiep tuc la source of truth.

Co the them sau:

- Qdrant neu can filter/rerank/vector operations manh hon trong production.
- OCR service cho scanned PDF.
- Dedicated reranker model.
- SQLite FTS hoac search engine rieng neu keyword search tro thanh nhu cau lon.
- Local LLM neu data policy khong cho goi cloud.

Khong nen lam ngay:

- Fine-tuning. RAG + corrections + eval nen lam truoc.
- De AI tu sua document/wiki ma khong co reviewer.
- Dua permission source of truth vao vector DB.

## 20. Definition of Done cho ban nang cap demo

Mot ban demo duoc xem la dat neu:

- LLM provider cau hinh duoc qua env/appsettings.
- Embedding la semantic embedding that, khong con mock hash.
- Q&A tra loi tieng Viet co citation dung.
- Retrieval filter theo quyen truoc khi lay context.
- AI noi ro khi thieu nguon.
- Wiki draft do LLM sinh co cau truc, khong copy 900 ky tu dau.
- Feedback sai tao quality issue.
- Reviewer approve correction.
- Lan hoi sau dung correction de tra loi tot hon.
- Co it nhat 5 demo questions pass.
- Co audit log cho generate/publish/correction.

## 21. Tai lieu tham khao

- OpenAI latest model guide: https://developers.openai.com/api/docs/guides/latest-model
- OpenAI structured outputs: https://developers.openai.com/api/docs/guides/structured-outputs
- OpenAI embedding model example: https://developers.openai.com/api/docs/models/text-embedding-3-large
- OpenAI retrieval overview: https://developers.openai.com/api/docs/guides/retrieval
- Chroma metadata filtering: https://docs.trychroma.com/docs/querying-collections/metadata-filtering
- Chroma data/retrieval overview: https://docs.trychroma.com/docs/overview/introduction
- Qdrant filtering: https://qdrant.tech/documentation/search/filtering/
