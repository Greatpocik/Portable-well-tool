using System;
using System.Collections.Generic;
using System.Text;

namespace WellTool.Model
{
    public static class Queries
    {
        //1. ИНФОРМАЦИЯ ПО СКВАЖИНЕ
        // 1. БАЗОВЫЙ ЗАПРОС (без WHERE)
        public static string GetWellInfoBase()
        {
            return @"
        SELECT 
            To_char(id) as Id,
            ""скв"" as Name,
            ""куст"" as Pad,
            ""цех"" as Workshop,
            ""днс"" as DNS,
            ""месторождение"" as Field,
            ""характер цдс"" as CdsCharacter,
            ""характер оис"" as OisCharacter,
            ""фонд оис"" as OisFund,
            ""REGION_ID"" as RegionId
        FROM system.jiv_skv";
        }

        // 2. СТАРЫЙ МЕТОД (для обратной совместимости)
        public static string GetWellInfo()
        {
            return GetWellInfoBase() +
                   @"
        WHERE UPPER(""скв"") LIKE UPPER(:searchText) 
           OR TO_CHAR(id) LIKE :searchText";
        }


        // 2. ЖУРНАЛ СОБЫТИЙ
        public static string GetEventJournal(string wellId)
        {
            return @"
                SELECT 
                    TO_CHAR(Начало,'DD.MM.YYYY HH24:MI:SS') AS ""Начало"",
                    TO_CHAR(Конец, 'DD.MM.YYYY HH24:MI:SS') AS ""Конец"",
                    Состояние AS ""Состояние"",
                    Вид_ремонта AS ""Вид ремонта"",
                    Вид_ГТМ AS ""Вид ГТМ"",
                    Причина_остановки AS ""Причина остановки"",
                    Вид_Потерь AS ""Вид потерь""
                FROM (
                    SELECT 
                        fj.start_date AS Начало,
                        fj.stop_date AS Конец,
                        fj.state_name AS Состояние,
                        fj.worcover_name AS Вид_ремонта,
                        fj.gtm_name AS Вид_ГТМ,
                        fj.stop_reason AS Причина_остановки,
                        fj.type_name AS Вид_Потерь
                    FROM frm_journal fj 
                    WHERE fj.well_id = :wellId
                    
                    UNION ALL 
                    
                    SELECT 
                        pwe.event_date AS Начало,
                        pwe.event_date_next AS Конец,
                        DECODE(pwe.event,'2','остановка ВЗ', '1', 'запуск ВЗ', 'ХЗ') AS Состояние,
                        pwe.comments AS Вид_ремонта,
                        null, null, null
                    FROM ppd_waterwell_event pwe, ppd_waterwells pw 
                    WHERE pw.id = pwe.waterwell_id 
                      AND pw.wellop_id = :wellId
                ) tt 
                ORDER BY tt.Начало DESC";
        }

        // 3. СОБЫТИЯ ИЗ ПУЛЬТА
        public static string GetPultEvents(string wellId)
        {
            return @"
                SELECT 
                    well_id,
                    TO_CHAR(enter_date,'DD.MM.YYYY HH24:MI:SS') AS ""Введеная дата"",
                    TO_CHAR(event_date,'DD.MM.YYYY HH24:MI:SS') AS ""дата события"",
                    name AS ""статус"",
                    event AS ""тип события"",
                    is_tm_data AS ""тм""
                FROM JIV_SKV_PULT 
                WHERE well_id = :wellId";
        }

        // 4. СОБСТВЕННИК ОБОРУДОВАНИЯ
        public static string GetEquipmentOwner(string wellId)
        {
            return @"
                SELECT 
                    NVL(c.ne_1, 'ЮНГ') AS ""Собственник"",
                    TO_CHAR(
                        TO_DATE(
                            dz_1 || LPAD(TRUNC(tz_1 / 60), 2, '0') || LPAD(MOD(tz_1, 60), 2, '0'), 
                            'YYYYMMDDHH24MI'
                        ), 
                        'dd.mm.rrrr hh24:mi:ss'
                    ) AS ""Дата начала"",
                    TO_CHAR(
                        TO_DATE(s.TK_1, 'yyyymmddHH24MI'), 
                        'dd.mm.yyyy HH24:MI'
                    ) AS ""Последняя дата ред.""
                FROM db_spons s 
                JOIN class c ON s.PRA_1 = c.cd_1  
                WHERE s.SK_1 = :wellId 
                  AND s.TK_1 > 200106290955  
                ORDER BY dz_1 DESC, tz_1 DESC";
        }

        // 5. РЕЕСТР: ИСТОРИЯ ПОСТАНОВКИ БРИГАДЫ
        public static string GetBrigadeHistory(string wellId)
        {
            return @"
                SELECT 
                    reestr_id AS ""ID ремонта"",
                    begin_date AS ""Начало периода"",
                    nm AS ""Вид мероприятия"",
                    short_name AS ""Вид ремонта"",
                    end_date AS ""Конец периода"",
                    podrad AS ""Подрядчик"",
                    brig AS ""Бригада"",
                    prich AS ""Причина снятия бригады""
                FROM jiv_tkrs_story 
                WHERE well_id = :wellId";
        }

        // 6. РЕЕСТР: ПОСУТОЧНОЕ НАХОЖДЕНИЕ СКВ
        public static string GetDailyStatus(string wellId)
        {
            return @"
                SELECT 
                    trn.id AS ""ID ремонта"",
                    trn.data AS ""Дата"",
                    trs.short_name AS ""Закладка"",
                    trn.oil_rate_calc AS ""Qн расч"",
                    trn.liq_rate_calc AS ""Qж расч"",
                    trn.stop_date AS ""Дата остановки"",
                    trn.end_date_fact AS ""Факт окон мер""
                FROM tkrs_reestr_new trn 
                INNER JOIN tkrs_reestr_status trs ON trs.id = trn.status_id 
                WHERE trn.well_id = :wellId 
                ORDER BY trn.data DESC";
        }

        // 7. РЕЕСТР: ЛОГ РЕМОНТА
        public static string GetRepairLog(string reestrId)
        {
            return @"
                SELECT 
                    TO_CHAR(t.event_date,'DD.MM.YYYY HH24:MI:SS') AS ""Дата внесения"",
                    TO_CHAR(t.data,'DD.MM.YYYY') AS ""Дата"",
                    t.object_name AS ""Пользователь"",
                    t.action AS ""Действие"",
                    t.message AS ""Примечание""
                FROM tkrs_log t 
                WHERE reestr_id = :reestrId 
                ORDER BY t.event_date DESC";
        }

        // 8. СПИСОК ПОДРЯДЧИКОВ ТКРС
        public static string GetContractorsList()
        {
            return @"
                SELECT 
                    id,
                    name_short AS ""Имя сокращ"",
                    name AS ""Имя полное"",
                    include_date AS ""Дата начала"",
                    exclude_date AS ""Дата окончания"",
                    DECODE(is_active,1,'Да', 'Нет') AS ""Активность""
                FROM DEPARTMENT 
                WHERE parent_id = '1000420' 
                ORDER BY name_short";
        }

        // 9. ПРИЧИНЫ ПЕРЕВОДА ПЕРИОДИКУ
        public static string GetTransferReasons(string wellId)
        {
            return "SELECT * FROM JIV_SOST_RR WHERE well_id = :wellId";
        }

        // 10. ГТМ: ПЛАНЫ
        public static string GetGtmPlans(string wellId)
        {
            return @"
                SELECT 
                    g.calc_month AS ""Расчетный месяц"",
                    DECODE(g.is_above_plan, 0, 'в мероп', 'вне мер') AS ""В мероп."",
                    (SELECT wt.short_name FROM workover_type wt WHERE wt.id = g.workover_type_id) AS ""Мероприятие"",
                    g.insert_date AS ""Дата добавления"",
                    DECODE(g.is_manual, 1, 'руч.', 'авт.') AS ""Метод"",
                    (SELECT gt.name FROM GTM_TABS gt WHERE gt.id = g.tab_id) AS ""Закладка"",
                    g.before_oil_density AS ""Плотность до мер."",
                    g.after_oil_density AS ""Плотность после мер."",
                    g.well_id,
                    g.id
                FROM gtm_well g
                WHERE g.well_id = :wellId
                ORDER BY g.calc_month DESC";
        }

        // 11. УДАЛИТЬ РЕМОНТ (хранимая процедура)
        public static string DeleteRepair(string id)
        {
            return @"
                BEGIN 
                    Tkrs_Reestr_Pkg.DeleteRemont(:id);
                    COMMIT; 
                END;";
        }

        // 12. ПРОВЕРКА ЗАПУСКА JOB
        public static string CheckJobStatus()
        {
            return @"
                SELECT TO_CHAR(THIS_DATE, 'dd.mm.rrrr hh24:mi:ss') AS d
                FROM user_jobs 
                WHERE JOB = 3228329";
        }
        // 13.1 Проверка журнала
        public static string CheckEventLog()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 
                    FROM  frm_journal fj 
                    WHERE fj.well_id = :wellId
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }

        // 13.2 ПРОВЕРКА СОБЫТИЙ ПУЛЬТА
        public static string CheckPultEvent()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 
                    FROM  operating_stop_start oss 
                    WHERE oss.well_id = :wellId 
                      AND oss.operating_status_id != 3
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }

        // 13.3 ПРОВЕРКА РЕЕСТРА ТКРС (активные ремонты)
        public static string CheckTKRS()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 
                    FROM  tkrs_reestr_new trn 
                    WHERE trn.well_id = :wellId 
                      AND trn.status_id != 6
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }

        // 13.4 ПРОВЕРКА НАЛИЧИЯ БРИГАД (для "требуется удалить ремонт")
        public static string CheckTKRSRigs()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 
                    FROM  tkrs_rigs_moving trm 
                    WHERE trm.well_id = :wellId
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }

        // 13.5 ПРОВЕРКА ГТМ (последние 30 дней)
        public static string CheckGTM()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 
                    FROM  gtm_well gw 
                    WHERE gw.well_id = :wellId 
                      AND gw.calc_month > SYSDATE - 30
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }

        // 13.6 ПРОВЕРКА ТЕХНОЛОГИЧЕСКИХ РАЗРЯДОК
        public static string CheckTehnRazr()
        {
            return @"
            SELECT CASE 
                WHEN EXISTS (
                    SELECT 1 FROM  tehn_razr tr WHERE tr.well_id = :wellId
                ) THEN 1 
                ELSE 0 
            END as result 
            FROM DUAL";
        }
    }
}
