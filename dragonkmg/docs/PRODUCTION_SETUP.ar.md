# تجهيز بيئة التجربة اليدوية

الهدف من هذا الدليل تشغيل الخدمات المتاحة محليًا بوضوح، لا وصف النسخة بأنها جاهزة للإنتاج. لا تحفظ أي مفاتيح أو كلمات مرور في Git.

## التشغيل المحلي الحالي

- التخزين الفعلي هو SQLite، ويُحدد ملفه عبر `DRAGONKMG_DB_PATH`.
- المصادقة المحلية تستخدم `DRAGONKMG_LOCAL_API_TOKEN` خارج بيئة Production.
- PostgreSQL غير مدعوم بعد. إذا عيّنت `DRAGONKMG_DB_PROVIDER=postgresql` فسيرفض التطبيق البدء بدل تجاهل الإعداد واستخدام SQLite.
- OIDC متاح عند ضبط `Authentication:Oidc`، وبيئة Production ترفض البدء إن لم يُفعّل OIDC بإعدادات HTTPS صالحة.
- MCP Streamable HTTP متاح عبر سجل ثابت محلي مع منح أداة صريح لكل وكيل.
- النسخ الاحتياطي يدعم إنشاء/عرض نسخ SQLite والتحقق من سلامتها. استعادة النسخ غير متاحة عبر API بعد.

ملف `appsettings.Production.example.json` لا يغيّر بيئة الاستضافة بذاته. لا تضبط `ASPNETCORE_ENVIRONMENT=Production` قبل استبدال قيم OIDC التجريبية بإعداد حقيقي؛ عندها فقط سيبدأ التطبيق مع OIDC والنطاق الإلزامي.

## تشغيل تجربة محلية

من مجلد `src/DragonKMG.Api`، عيّن رمزًا عشوائيًا جديدًا في طرفية محلية فقط، ثم شغّل API. لا تضع الرمز في أوامر محفوظة أو لقطات شاشة أو سجلات.

```powershell
$env:DRAGONKMG_LOCAL_API_TOKEN = Read-Host "Local API token"
$env:DRAGONKMG_DB_PATH = Join-Path $env:TEMP "dragonkmg-manual-trial.db"
$env:DRAGONKMG_BACKUP_DIRECTORY = Join-Path $env:TEMP "dragonkmg-backups"
dotnet run --project .\DragonKMG.Api.csproj
```

أرسل الرمز في ترويسة `X-DragonKMG-Local-Token` لطلبات `/api/*`. نقطة `/health` لا تتطلب تسجيل دخول. نقطة `/api/operations/readiness` تعرض حالة المكونات دون أسرار. لا تستخدم بيانات حقيقية قبل إعداد النسخ الاحتياطي واختبار استعادتها يدويًا.

## توصيل OIDC

اضبط القيم التالية في مخزن أسرار البيئة أو إعداد محلي خارج Git، ثم شغّل التطبيق في بيئة غير Production للتجربة الأولى. عند `Enabled=true` تُتحقق صحة issuer وaudience والتوقيع والعمر من مفاتيح مزود OIDC عبر HTTPS.

```text
Authentication__Oidc__Enabled=true
Authentication__Oidc__Authority=https://عنوان-المزود
Authentication__Oidc__Issuer=https://issuer-المتوقع
Authentication__Oidc__Audience=dragonkmg-api
Authentication__Oidc__RequiredScope=dragonkmg.api
Authentication__Oidc__RequireHttpsMetadata=true
```

مع OIDC مفعّل، تُقبل طلبات API عبر `Authorization: Bearer <access-token>`. في Production يكون OIDC إلزاميًا، ولا يقبل التطبيق الرمز المحلي كبديل.
يجب أن يحتوي الرمز أيضًا على النطاق المحدد في `RequiredScope`؛ لا توجد حتى الآن مطابقة claims إلى مستخدمين/أدوار أو عزل مستأجرين، لذلك هذا إعداد للتجربة وليس اعتمادًا متعدد المستخدمين للإنتاج.

## توصيل MCP يدويًا

أنشئ ملف `mcp-servers.json` محليًا خارج Git، واضبط `DRAGONKMG_MCP_REGISTRY` لمساره. المثال لا يتضمن أسرار مصادقة؛ العميل الحالي لا يرسل بيانات اعتماد إلى خوادم MCP، لذا لا تستخدم endpoint يتطلب سرًا أو صلاحيات كتابة.

```json
{
  "servers": [
    { "id": "local-demo", "endpoint": "http://127.0.0.1:9000/mcp", "enabled": true }
  ],
  "grants": [
    { "agentId": "REPLACE_WITH_AGENT_ID", "serverId": "local-demo", "toolName": "list_items" }
  ]
}
```

يسمح HTTP على loopback فقط؛ غير ذلك HTTPS فقط. اكتشاف الأدوات يتم عبر `POST /api/mcp/{serverId}/discover`. لاستدعاء أداة استخدم `POST /api/mcp/{serverId}/tools/{toolName}/call` مع `{ "agentId": "<existing-agent-guid>", "arguments": {} }`. استخدم GUID وكيل موجود ومفعّل، وأضف Grant مطابقًا في الملف. مع OIDC يلزم أيضًا `DragonKMG__Mcp__ExecuteScope` في token؛ حاملوه مشغلون مخولون لاختيار أي وكيل ممنوح، وليس عزلًا لهوية كل مستخدم. أما local token فهو هوية مشغل كاملة للتجربة فقط. الاعتماد/الهوية متعددة المستخدمين وربط التنفيذ بموافقات الأفعال ذات الأثر ما زالا غير مكتملين.

## النسخ الاحتياطي

اضبط `DRAGONKMG_BACKUP_DIRECTORY` إلى مجلد محلي محمي. استخدم `GET /api/operations/backups` لعرض النسخ و`POST /api/operations/backups` لإنشاء نسخة online متسقة والتحقق من `integrity_check`. المجلد قد يحتوي بيانات محادثات ومشاريع؛ امنع مشاركته أو رفعه للمستودع. لا توجد استعادة API حتى الآن.

## ما تبقى قبل Production عامة

- بناء PostgreSQL adapter مكافئ لكل عمليات التخزين وترحيل مخزن GitHub إليه، ثم تفعيل migrations واختبارات تكافؤ.
- استعادة نسخ احتياطية آمنة ومختبرة، وسياسة احتفاظ وتشفير وتحقق دوري.
- دعم اعتماد MCP بأسرار محفوظة آمنًا، وربط الاستدعاء الفعلي بحوكمة الموافقات والتدقيق.
- اختبار مزود OIDC فعلي، وتحديد خرائط المستخدمين/الأدوار وصلاحيات المستأجرين.
- مراجعة أمنية واختبارات تكامل ونشر فعلية على بيئتك.
