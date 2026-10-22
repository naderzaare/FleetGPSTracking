# راهنمای آزمایشی GPS Receiver

این مرحله فقط دریافت TCP دستگاه Teltonika FMB920 و ذخیره فایل است. RabbitMQ، SQL Server، نقشه، رابط کاربری و تحلیل مسیر در آن وجود ندارد. شبیه‌ساز قبلی در مخزن نبود؛ ابزار جدید با کتابخانه استاندارد Python اضافه شده است.

## ۱. ثبت و ارسال مخزن توسط شما در Windows

در PowerShell و پوشه پروژه، ابتدا تغییرات را بررسی کنید. فایل capture و اطلاعات واقعی دستگاه را وارد Git نکنید. یک مخزن خالی در GitHub بسازید و نشانی واقعی خود را جایگزین کنید:

```powershell
git status
git diff
dotnet run --project tests/GpsReceiver.Tests -c Release
git add .gitignore .dockerignore src/GpsReceiver tests/GpsReceiver.Tests tools/GpsSimulator deploy docs/gps-receiver-fa.md docs/gps-receiver-protocol.md docs/gps-receiver-validation.md
git commit -m "Add standalone Teltonika GPS Receiver experiment"
git remote -v
# فقط اگر origin وجود ندارد:
git remote add origin https://github.com/YOUR_ACCOUNT/FleetGPSTracking.git
git branch --show-current
# اگر checkout بدون شاخه است، ابتدا:
git switch -c codex/gps-receiver
git push -u origin HEAD
```

اگر روی شاخه هستید، دستور ساخت شاخه لازم نیست. برای مخزن خصوصی از SSH یا روش احراز هویت GitHub استفاده کنید؛ توکن را در URL ننویسید. این راهنما هیچ push یا deployment خودکاری انجام نمی‌دهد.

## ۲. آماده‌سازی Ubuntu و clone

Git، Python 3، Docker Engine و افزونه Docker Compose باید نصب باشند. نصب Docker را طبق مستند رسمی انجام دهید: https://docs.docker.com/engine/install/ubuntu/ . فرمان‌ها فرض می‌کنند Docker با sudo اجرا می‌شود.

```bash
sudo apt-get update
sudo apt-get install -y git python3
sudo docker version
sudo docker compose version
git clone https://github.com/YOUR_ACCOUNT/FleetGPSTracking.git
cd FleetGPSTracking
# نام شاخه‌ای که push کرده‌اید:
git switch codex/gps-receiver
sudo install -d -m 0700 -o 1654 -g 1654 /srv/fleet-gps/captures
sudo docker compose -f deploy/compose.yaml build
sudo docker compose -f deploy/compose.yaml up -d
sudo docker compose -f deploy/compose.yaml logs --tail=100 -f
```

شناسه 1654 کاربر غیرریشه تصویر رسمی .NET 8 است. خطای دسترسی به مسیر capture باعث شکست startup می‌شود. برای قطع نمایش log از Ctrl+C استفاده کنید؛ سرویس متوقف نمی‌شود. image برای build نیاز به اینترنت دارد. زمان‌های داخل فایل و log قرارداد ماشینی UTC هستند؛ این سرویس صفحه نمایش تاریخ ندارد.

## ۳. آزمایش شبیه‌ساز قبل از دستگاه واقعی

در ترمینال دوم و پوشه مخزن Ubuntu:

```bash
python3 tools/GpsSimulator/simulate.py --host 127.0.0.1 --port 5027 --fragment
sudo docker compose -f deploy/compose.yaml logs --tail=50
sudo ls -lh /srv/fleet-gps/captures
sudo sh -c 'tail -n 5 /srv/fleet-gps/captures/*.txt'
```

نتیجه مورد انتظار: PASS برای handshake و ACK یک رکورد؛ در log عبارت accepted و records=1 دیده می‌شود. رکورد نمونه رسمی عمداً مختصات معتبر ندارد؛ null بودن SpeedKph صحیح است و نشانه توقف خودرو نیست. IMEI شبیه‌ساز ساختگی و تماماً صفر است.

آزمایش ماندگاری: نام فایل‌ها را یادداشت کنید، کانتینر را بازسازی کنید و دوباره همان فایل‌ها را ببینید:

```bash
sudo docker compose -f deploy/compose.yaml up -d --force-recreate
sudo ls -lh /srv/fleet-gps/captures
python3 tools/GpsSimulator/simulate.py --fragment
```

مسیر میزبان مستقل از عمر کانتینر است؛ حذف دستی پوشه میزبان داده‌ها را حذف می‌کند. فایل‌ها خودکار حذف نمی‌شوند. فضای دیسک را با `df -h /srv/fleet-gps/captures` کنترل و از آن نسخه پشتیبان تهیه کنید.

## ۴. اتصال FMB920 واقعی

۱. سیم‌کارت، PIN/APN، آنتن و دسترسی دیتای دستگاه را بررسی کنید.
۲. با Teltonika Configurator متناسب با firmware، APN اپراتور و نشانی IP یا DNS قابل دسترس سرور را در تنظیمات GPRS وارد کنید؛ پورت 5027، پروتکل TCP و Codec 8 یا Codec 8 Extended انتخاب شود. تنظیمات را ذخیره و بازخوانی کنید. در صورت استفاده از SMS، سند موجود Teltonika_FMB920_SMS_Commands_Codex.md را با firmware دستگاه تطبیق دهید؛ این سرویس دستور دستگاه ارسال نمی‌کند.
۳. در صورت وجود NAT، پورت TCP 5027 را به Ubuntu منتقل کنید. فایروال میزبان/شبکه را برای منبع لازم باز کنید. شبکه موبایل باید بتواند به endpoint برسد؛ IP خصوصی LAN معمولاً از سیم‌کارت عمومی قابل دسترس نیست. VPN یا APN خصوصی گزینه مناسب شبکه کنترل‌شده است.
۴. پس از تست شبیه‌ساز، فهرست IMEIهای مجاز را در فایل خصوصی `deploy/.env` تنظیم کنید: `GPS_ALLOWED_IMEIS=YOUR_REAL_IMEI`؛ چند شناسه با ویرگول جدا می‌شوند. این فایل در Git نرود. سپس سرویس را دوباره ایجاد کنید:

```bash
sudo docker compose --env-file deploy/.env -f deploy/compose.yaml up -d --force-recreate
sudo docker compose -f deploy/compose.yaml logs --tail=100 -f
```

۵. دستگاه را در محل دارای دید آسمان روشن کنید. connected یعنی اتصال TCP برقرار شده؛ accepted یعنی packet معتبر ذخیره و ACK ارسال شده است. DeviceUtc، serverUtc، Satellites و PositionUsable را در capture بررسی کنید. داده با timestamp قدیمی می‌تواند بافر آفلاین دستگاه باشد و نباید با موقعیت زنده اشتباه شود.
۶. پذیرش سخت‌افزار: دریافت چند packet معتبر، قطع و اتصال مجدد شبکه، دریافت داده‌های بافرشده، بررسی UTC و مختصات و ماندگاری capture پس از بازسازی کانتینر را ثبت کنید. این آزمایش واقعی هنوز اینجا اجرا نشده است.

## ۵. فایل‌ها، عیب‌یابی و محدودیت‌ها

هر اتصال فایل `.txt` جداگانه با JSON در هر خط تولید می‌کند؛ Word نیست. rx شامل hex هر قطعه دریافت‌شده حتی packet ناقص/خراب است؛ packet شامل rawHex کامل، IMEI، زمان دریافت سرور و رکوردهای decode شده است؛ tx پاسخ واقعی سرور را ثبت می‌کند. فایل‌ها حاوی اطلاعات حساس مکان و شناسه هستند؛ برای گزارش معمول فقط session و تعداد رکوردها را به اشتراک بگذارید. log کنسول IMEI و مختصات ندارد.

- connected بدون accepted: تنظیم codec، CRC، طول packet، قطع ارتباط یا timeout را بررسی کنید. reason و detail در log ثبت می‌شود.
- هیچ connected: نشانی سرور، TCP/NAT، فایروال، APN و پوشش سیم‌کارت بررسی شود. نبود داده علت قطعی برق یا خرابی دستگاه را اثبات نمی‌کند.
- permission یا IOException: مجوز و فضای bind mount بررسی شود. packet با خطای ذخیره ACK نمی‌شود؛ دستگاه ممکن است آن را تکرار کند.
- تنظیمات محیطی: GPS_PORT=5027، GPS_CAPTURE_DIR، GPS_IDLE_SECONDS=180، GPS_MAX_CONNECTIONS=100 و GPS_MAX_FRAME_BYTES=65536. سقف طول یک محافظ عملیاتی است، نه ادعای حداکثر سخت‌افزار. deadline برای تکمیل هر بخش frame است.
- این نسخه TCP و Codec 8/8E دارد؛ UDP، Codec 12/16، ارسال فرمان، TLS، حذف تکرارها، احراز هویت رمزنگاری‌شده و تضمین ظرفیت 1000 دستگاه ندارد. IMEI مجاز به‌تنهایی احراز هویت امن نیست. endpoint را در شبکه کنترل‌شده آزمایش کنید.
- فایل قبل از ACK flush به دیسک می‌شود؛ سلامت خود دیسک/ذخیره‌ساز تضمین نمی‌شود. تکرارها عمداً حفظ می‌شوند؛ قطع پس از ذخیره و قبل از دریافت ACK می‌تواند تکرار بسازد. ACK به معنی ذخیره capture است، نه پردازش تجاری.
- IOها با شناسه و hex بدون تفسیر سنسور ذخیره می‌شوند؛ از GPS وضعیت موتور، سوخت یا قطع برق استنباط نمی‌شود. PositionUsable فقط یک کنترل اولیه کیفیت است.
- کنترل سلامت HTTP وجود ندارد؛ وضعیت running به تنهایی صحت دریافت دستگاه را اثبات نمی‌کند. log و تست ACK و فایل را بررسی کنید.

توقف و راه‌اندازی:

```bash
sudo docker compose -f deploy/compose.yaml stop
sudo docker compose -f deploy/compose.yaml start
```

اجرای محلی توسعه (SDK .NET 8):

```bash
dotnet run --project tests/GpsReceiver.Tests -c Release
dotnet run --project src/GpsReceiver -c Release
```

تست‌ها یک برنامه مستقل بدون بسته خارجی هستند؛ با `dotnet run` اجرا می‌شوند، نه `dotnet test`. Docker در محیط پیاده‌سازی موجود نبود؛ build و اجرای Linux container باید روی Ubuntu طبق مراحل بالا تایید شود.

