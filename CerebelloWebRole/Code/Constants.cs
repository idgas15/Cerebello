﻿namespace CerebelloWebRole.Code
{
    /// <summary>
    /// Constants
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// The max size for "names" in the database
        /// </summary>
        public const int DB_NAME_MAX_LENGTH = 200;

        /// <summary>
        /// The size of a page of the grid
        /// </summary>
        public const int GRID_PAGE_SIZE = 20;

        /// <summary>
        /// The default password given to every new user.
        /// When loggin in with this password the user will be asked to change the password.
        /// The user is not allowed to use this passwrod.
        /// </summary>
        public const string DEFAULT_PASSWORD = "123abc";

#if DEBUG
        public const string DOMAIN = "localhost";
#else
        public const string DOMAIN = "cerebello.com.br";
#endif

        public const string EMAIL_POWEREDBY = "www.cerebello.com.br";

#if DEBUG
        public static readonly int? PORT = 12621;
#else
        public static readonly int? PORT = null;
#endif
    }
}
