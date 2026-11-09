using System;
using System.Collections.Generic;
using System.Text;

namespace WellTool.Model
{
    public class WellData

    {

        public string Id { get; set; } = string.Empty;    // id
        public string Name { get; set; } = string.Empty; // скв (varchar2)
        public string Pad { get; set; } = string.Empty;  // куст (varchar2)
        public string Workshop { get; set; } = string.Empty; // цех (varchar2)
        public string DNS { get; set; } = string.Empty;   // днс (varchar2)
        public string Field { get; set; } = string.Empty; // месторождение (varchar2)
        public string CdsCharacter { get; set; } = string.Empty; // характер цдс
        public string OisCharacter { get; set; } = string.Empty; // характер оис
        public string OisFund { get; set; } = string.Empty; // фонд оис
        public string RegionId { get; set; } = string.Empty; // region_id

        public bool IsSelected { get; set; }

    }
    
    public class DataPresenceCheck
    {
        public string WellId { get; set; } = string.Empty;
        public bool HasEventLog { get; set; }        // true/false вместо 1/0
        public bool HasPultEvent { get; set; }       // true/false
        public bool HasTKRS { get; set; }            // true/false
        public bool RequiresDeleteRemont { get; set; } // true/false
        public bool HasGTM { get; set; }             // true/false
        public bool HasTehnRazr { get; set; }        // true/false

        // ВЫЧИСЛЯЕМОЕ СВОЙСТВО: готова к удалению
        public bool IsReadyToDelete =>
            !(HasEventLog || HasPultEvent || HasTKRS || HasGTM || HasTehnRazr);
        
    }
    public class SearchCondition
    {
        public string Column { get; set; } = "Скважина"; // Столбец по умолчанию
        public string Value { get; set; } = string.Empty;
        public bool IsFirst { get; set; } = false;
    }

}

