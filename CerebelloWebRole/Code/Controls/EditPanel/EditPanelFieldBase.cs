﻿using System;

namespace CerebelloWebRole.Code.Controls
{
    public class EditPanelFieldBase
    {
        /// <summary>
        /// formato do conteúdo
        /// </summary>
        public Func<dynamic, object> Format { get; protected set; }

        /// <summary>
        /// Formato da descrição
        /// </summary>
        public Func<dynamic, object> FormatDescription { get; protected set; }

        public String Header { get; protected set; }
        public EditPanelFieldSize Size { get; protected set; }

        /// <summary>
        /// ಠ.ಠ
        /// Determina se este campo aparece sozinho na linha
        /// </summary>
        public bool WholeRow { get; protected set; }
    }
}
