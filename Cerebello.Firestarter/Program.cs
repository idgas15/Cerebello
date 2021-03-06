﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.Objects.DataClasses;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Cerebello.Firestarter.Helpers;
using Cerebello.Firestarter.SqlParser;
using Cerebello.Model;
using CerebelloWebRole.Code;
using File = System.IO.File;

namespace Cerebello.Firestarter
{
    // ReSharper disable LocalizableElement
    internal class Program
    {
        private static void Main(string[] args)
        {
            TypeDescriptor.AddAttributes(typeof(EntityObject), new TypeConverterAttribute(typeof(ExpandableObjectConverter)));

            new Program().Run();
        }

        private bool isFuncBackupEnabled;

        private string connName;

        private readonly string rootCerebelloPath = ConfigurationManager.AppSettings["CerebelloPath"];

        const int defaultSeed = 101; // default random seed
        private int? rndOption = defaultSeed;

        bool detachDbWhenDone;

        private void Run()
        {
            if (string.IsNullOrEmpty(this.rootCerebelloPath))
                throw new Exception("Cannot start FireStarter. Cannot find Cerebello root path configuration");

            bool isTestDb = false;

            bool isAzureDb = false;

            bool isToChooseDb = true;

            bool showHidden = false;

            Console.BufferWidth = 200;

            var lastVer = File.Exists("last.ver") ? File.ReadAllText("last.ver") : null;
            var currVer = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            File.WriteAllText("last.ver", currVer);

            // todo: move this to the fs.config file
            this.isFuncBackupEnabled = File.Exists("isFuncBackupEnabled");

            // New options:
            while (true)
            {
                // loading firestarter configuration file "fs.config"
                this.LoadConfigurations();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("Firestarter v{0}", currVer);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                if (lastVer != currVer)
                    Console.Write(" last used v{0}", lastVer ?? "?.?.?.?");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("DateTime (UTC):   {0}", Firestarter.UtcNow);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(Firestarter.UtcNow != DateTime.UtcNow ? " (with debug offset)" : "");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("DateTime (Local): {0}", Firestarter.Now);
                Console.ForegroundColor = ConsoleColor.DarkRed;
                if (Firestarter.UtcNow != DateTime.UtcNow)
                    Console.Write(" (offset = {0})", DebugConfig.CurrentTimeOffset);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine();
                Console.WriteLine("DateTime (?):     {0}", Firestarter.Now.ToUniversalTime());
                Console.WriteLine("CurrentDir:       {0}", Environment.CurrentDirectory);

                while (isToChooseDb)
                {
                    if (this.detachDbWhenDone && !isAzureDb)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("Detaching DB... ");

                        bool ok = false;
                        using (var db = this.CreateCerebelloEntities())
                            try
                            {
                                Firestarter.DetachLocalDatabase(db);
                                ok = true;
                            }
                            catch
                            {
                            }


                        if (ok)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("DONE");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR");
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Black;

                    // Getting available connections from ConfigurationManager so that user can choose one.
                    Console.WriteLine("Choose connection to use:");

                    var connStr = new Dictionary<string, string>();
                    for (int idxConnStr = 0; idxConnStr < ConfigurationManager.ConnectionStrings.Count; idxConnStr++)
                    {
                        var connStrSettings = ConfigurationManager.ConnectionStrings[idxConnStr];
                        connStr[idxConnStr.ToString(CultureInfo.InvariantCulture)] = connStrSettings.Name;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write("{0}: ", idxConnStr);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(connStrSettings.Name);
                    }
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                    Console.WriteLine(@"cls: Clear screen.");
                    Console.WriteLine(@"q:   Quit!");
                    Console.ForegroundColor = ConsoleColor.White;

                    Console.WriteLine();
                    Console.Write("Type the option and press Enter: ");

                    // User may now choose a connection.
                    int idx;
                    string userOption = Console.ReadLine().ToLowerInvariant().Trim();
                    if (!int.TryParse(userOption, out idx) || !connStr.TryGetValue(
                        idx.ToString(CultureInfo.InvariantCulture),
                        out this.connName))
                    {
                        if (userOption == "q" || userOption == "quit")
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Bye!");
                            return;
                        }

                        if (userOption == "cls")
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Clear();
                            continue;
                        }

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid option.");
                        Console.WriteLine();
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(this.connName))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Cannot connect because the connection string is empty.");
                        Console.WriteLine();
                        continue;
                    }

                    // commented code: code to correct when DB TYPE was changed from DATE to DATETIME
                    //using (var db = this.CreateCerebelloEntities())
                    //{
                    //    foreach (var accountContract in db.AccountContracts)
                    //    {
                    //        accountContract.StartDate = PracticeController.ConvertToUtcDateTime(accountContract.Practice, accountContract.StartDate);
                    //        accountContract.EndDate = PracticeController.ConvertToUtcDateTime(accountContract.Practice, accountContract.EndDate);
                    //        accountContract.IssuanceDate = PracticeController.ConvertToUtcDateTime(accountContract.Practice, accountContract.IssuanceDate);
                    //    }
                    //    db.SaveChanges();
                    //}

                    try
                    {
                        isTestDb = this.connName.ToUpper().Contains("TEST");

                        isAzureDb = this.connName.ToUpper().Contains("AZURE");

                        if (!isAzureDb)
                        {
                            using (var db = this.CreateCerebelloEntities())
                            {
                                var attachResult = Firestarter.AttachLocalDatabase(db);
                                if (attachResult == Firestarter.AttachLocalDatabaseResult.NotFound)
                                {
                                    // Create the DB if it does not exist.
                                    bool createDb = ConsoleHelper.YesNo("Would you like to create the database?");
                                    if (createDb)
                                    {
                                        Console.WriteLine();
                                        var result = Firestarter.CreateDatabaseIfNeeded(db);
                                        if (!result)
                                        {
                                            Console.WriteLine("Could not create database.");
                                            continue;
                                        }

                                        this.detachDbWhenDone = isTestDb;
                                    }
                                    else
                                        continue;
                                }
                                else
                                {
                                    this.detachDbWhenDone = attachResult == Firestarter.AttachLocalDatabaseResult.Ok;
                                }

                                if (this.detachDbWhenDone)
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Blue;
                                    Console.WriteLine("DB attached!");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.ConsoleWriteException(ex);
                        Console.WriteLine();
                        continue;
                    }

                    break;
                }

                isToChooseDb = false;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Current DB: {0}", this.connName);
                Console.WriteLine("Project Path: {0}", this.rootCerebelloPath);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(@"r      - Magic option: reset database (differentiates WORK and TEST database).");
                Console.WriteLine(@"db     - Change database.");

                if (showHidden)
                {
                    Console.WriteLine();
                    if (!isAzureDb)
                    {
                        Console.WriteLine(@"clr    - Clear all data.");
                        Console.WriteLine(@"p1     - Populate database with items (type p1? to know more).");
                        Console.WriteLine(@"drp    - Drop all tables and FKs.");
                        Console.WriteLine(@"crt    - Create all tables and FKs using script.");
                        Console.WriteLine();
                    }
                    Console.WriteLine(@"anvll  - Downloads all leaflets from Anvisa site.");
                    Console.WriteLine(@"rnd    - Set the seed to the random generator.");
                    if (!isAzureDb)
                    {
                        Console.WriteLine();
                        Console.WriteLine(@"bkc    - Create database backup.");
                        Console.WriteLine(@"bkr    - Restore database backup.");

                        Console.Write(@"abk    - Enables or disables functional backups. (");
                        Console.ForegroundColor = isFuncBackupEnabled ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
                        Console.Write(isFuncBackupEnabled ? "enabled" : "disabled");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(@")");
                    }

                    Console.WriteLine(@"eml    - Sends e-mails to users.");

                    Console.WriteLine();
                    if (!isAzureDb)
                    {
                        using (var db = this.CreateCerebelloEntities())
                        {
                            if (Firestarter.BackupExists(db, "__zero__"))
                                Console.WriteLine(@"zero   - Restores DB to last zeroed state.");
                            if (Firestarter.BackupExists(db, "__undo__"))
                                Console.WriteLine(@"undo   - Undoes the last operation (if possible).");
                            if (Firestarter.BackupExists(db, "__redo__"))
                                Console.WriteLine(@"redo   - Redoes something that was undone.");
                            if (Firestarter.BackupExists(db, "__reset__"))
                                Console.WriteLine(@"reset  - Reset DB to initial set (differentiates WORK and TEST).");
                        }
                        Console.WriteLine();
                        Console.WriteLine(
                            this.detachDbWhenDone
                                ? @"atc    - Leaves DB attached when done."
                                : @"dtc    - Detach DB when done.");
                    }
                    Console.WriteLine();
                    Console.WriteLine(@"cls    - Clear screen.");
                }

                Console.WriteLine(@"RETURN - {0} options.", showHidden ? "Hides visible" : "Shows hidden");

                Console.WriteLine(@"q      - Quit.");
                Console.WriteLine(@"       Type ? after any option to get help.");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.Write("What would you like to do with the DB: ");
                string userOption1 = Console.ReadLine();

                switch (userOption1.Trim().ToLowerInvariant())
                {
                    case "":

                        showHidden = !showHidden;

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(showHidden ? "Complex options: VISIBLE" : "Complex options: HIDDEN");

                        break;

                    case "abk?": if (!isAzureDb) InfoAbk(); break;
                    case "abk": if (!isAzureDb) this.OptAbk(); break;

                    case "undo?": if (!isAzureDb) InfoUndo(); break;
                    case "undo": if (!isAzureDb) this.OptUndo(); break;

                    case "zero?": if (!isAzureDb) InfoZero(); break;
                    case "zero": if (!isAzureDb) this.OptZero(); break;

                    case "redo?": if (!isAzureDb) InfoRedo(); break;
                    case "redo": if (!isAzureDb) this.OptRedo(); break;

                    case "reset?": if (!isAzureDb) InfoReset(); break;
                    case "reset": if (!isAzureDb) this.OptReset(); break;

                    case "atc?": if (!isAzureDb) InfoAtc(); break;
                    case "atc": if (!isAzureDb) this.OptAtc(); break;

                    case "dtc?": if (!isAzureDb) InfoDtc(); break;
                    case "dtc": if (!isAzureDb) this.OptDtc(); break;

                    case "crt?": if (!isAzureDb) InfoCrt(); break;
                    case "crt": if (!isAzureDb) this.OptCrt(); break;

                    case "size?": InfoSize(); break;
                    case "size": this.OptSize(); break;

                    case "drp?": if (!isAzureDb) InfoDrp(); break;
                    case "drp": if (!isAzureDb) this.OptDrp(); continue;

                    case "cls?": InfoCls(); break;
                    case "cls": OptCls(); break;

                    case "clr?": if (!isAzureDb) InfoClr(); break;
                    case "clr": if (!isAzureDb) this.OptClr(); continue;

                    case "p1?": if (!isAzureDb) InfoP1(); break;
                    case "p1": if (!isAzureDb) this.OptP1(); break;

                    case "anvll?": InfoAnvll(); break;
                    case "anvll": this.OptAnvll(); break;

                    case "q?": InfoQ(); break;
                    case "q": this.OptQ(); return;

                    case "db?": InfoDb(); break;
                    case "db": isToChooseDb = OptDb(); break;

                    case "rnd?": InfoRnd(); break;
                    case "rnd": this.OptRnd(); break;

                    case "bkc?": if (!isAzureDb) InfoBkc(); break;
                    case "bkc": if (!isAzureDb) this.OptBkc(); break;

                    case "bkr?": if (!isAzureDb) InfoBkr(); break;
                    case "bkr": if (!isAzureDb) this.OptBkr(); break;

                    case "r?": if (!isAzureDb) InfoR(); break;
                    case "r": this.OptR(); continue;

                    case "eml?": InfoEml(); break;
                    case "eml": this.OptEml(); break;

                    case "exec": this.OptExec(); break;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid option.");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }
            }
        }

        private CerebelloEntities CreateCerebelloEntities()
        {
            return new CerebelloEntities(string.Format("name={0}", this.connName));
        }

        private static void InfoAbk()
        {
        }

        private void OptAbk()
        {
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("e: enabled; d: disabled; anything else: only show status");
                    Console.Write("Type option: ");
                    var key = Console.ReadKey().KeyChar;
                    Console.WriteLine();
                    if (key == 'e' || key == 'd')
                    {
                        this.isFuncBackupEnabled = key == 'e';

                        if (this.isFuncBackupEnabled)
                            File.Create("isFuncBackupEnabled").Close();
                        else
                            File.Delete("isFuncBackupEnabled");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.ConsoleWriteException(ex);
                }
                finally
                {
                    this.isFuncBackupEnabled = File.Exists("isFuncBackupEnabled");
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Functional backups is ");
                Console.ForegroundColor = this.isFuncBackupEnabled ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
                Console.WriteLine(this.isFuncBackupEnabled ? "enabled" : "disabled");
                Console.ForegroundColor = ConsoleColor.White;

                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private static void InfoUndo()
        {
        }

        private void OptUndo()
        {
            using (var db = this.CreateCerebelloEntities())
            {
                if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__redo__");
                var fileName = Firestarter.RestoreBackup(db, "__undo__");
                if (fileName != null)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("File name: {0}", fileName);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Undone!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Could not undo!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        private static void InfoZero()
        {
        }

        private void OptZero()
        {
            using (var db = this.CreateCerebelloEntities())
            {
                if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                var fileName = Firestarter.RestoreBackup(db, "__zero__");
                if (fileName != null)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("File name: {0}", fileName);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Zeroed!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Could not zero database!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        private static void InfoRedo()
        {
        }

        private void OptRedo()
        {
            using (var db = this.CreateCerebelloEntities())
            {
                var fileName = Firestarter.RestoreBackup(db, "__redo__");
                if (fileName != null)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("File name: {0}", fileName);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Could not redo!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        private static void InfoReset()
        {
        }

        private void OptReset()
        {
            using (var db = this.CreateCerebelloEntities())
            {
                if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                var fileName = Firestarter.RestoreBackup(db, "__reset__");
                if (fileName != null)
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("File name: {0}", fileName);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Could not reset database!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        private static void InfoAtc()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("When you are done with the current DB, it will be left attached.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptAtc()
        {
            this.detachDbWhenDone = false;
        }

        private static void InfoDtc()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("When you are done with the current DB, it is going to be detached.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptDtc()
        {
            this.detachDbWhenDone = true;
        }

        private static void InfoCrt()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine(@"Loads the file '\DB\Scripts\script.sql',");
                Console.WriteLine("Changes the collation of columns in the script to 'Latin1_General_CI_AI',");
                Console.WriteLine("and then executes the changed script.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptCrt()
        {
            {
                try
                {
                    using (var db = this.CreateCerebelloEntities())
                    {
                        if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                        this.CreateDatabaseUsingScript(db);
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                catch (Exception ex)
                {
                    ConsoleHelper.ConsoleWriteException(ex);
                }
            }
        }

        private static void InfoSize()
        {
        }

        private void OptSize()
        {
            using (var db = this.CreateCerebelloEntities())
            {
                Console.WriteLine(
                    "SYS_MedicalEntity: Code.Length: min={0}; max={1}",
                    db.SYS_MedicalEntity.Min(x => x.Code.Length),
                    db.SYS_MedicalEntity.Max(x => x.Code.Length));
                Console.WriteLine(
                    "SYS_MedicalEntity: Name.Length: min={0}; max={1}",
                    db.SYS_MedicalEntity.Min(x => x.Name.Length),
                    db.SYS_MedicalEntity.Max(x => x.Name.Length));
                Console.WriteLine(
                    "SYS_MedicalProcedure: Code.Length: min={0}; max={1}",
                    db.SYS_MedicalProcedure.Min(x => x.Code.Length),
                    db.SYS_MedicalProcedure.Max(x => x.Code.Length));
                Console.WriteLine(
                    "SYS_MedicalProcedure: Name.Length: min={0}; max={1}",
                    db.SYS_MedicalProcedure.Min(x => x.Name.Length),
                    db.SYS_MedicalProcedure.Max(x => x.Name.Length));
                Console.WriteLine(
                    "SYS_MedicalSpecialty: Code.Length: min={0}; max={1}",
                    db.SYS_MedicalSpecialty.Min(x => x.Code.Length),
                    db.SYS_MedicalSpecialty.Max(x => x.Code.Length));
                Console.WriteLine(
                    "SYS_MedicalSpecialty: Name.Length: min={0}; max={1}",
                    db.SYS_MedicalSpecialty.Min(x => x.Name.Length),
                    db.SYS_MedicalSpecialty.Max(x => x.Name.Length));
            }
        }

        private static void InfoDrp()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Methods that will be called:");
                Console.WriteLine("    DropAllTables");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptDrp()
        {
            {
                var clearAllData = ConsoleHelper.YesNo("This will drop EVERY table in your DB... are you sure?");

                // Doing what the user has told.
                using (var db = this.CreateCerebelloEntities())
                {
                    if (clearAllData)
                    {
                        if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                        Firestarter.DropAllTables(db);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Canceled!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
        }

        private static void InfoCls()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Clears the output from all previous commands.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private static void OptCls()
        {
            Console.Clear();
        }

        private static void InfoClr()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Methods that will be called:");
                Console.WriteLine("    ClearAllData");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptClr()
        {
            {
                var clearAllData = ConsoleHelper.YesNo("Clear all data?");

                // Doing what the user has told.
                using (var db = this.CreateCerebelloEntities())
                {
                    if (clearAllData)
                    {
                        if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                        try
                        {
                            Firestarter.ClearAllData(db);
                        }
                        catch (Exception ex)
                        {
                            ConsoleHelper.ConsoleWriteException(ex);
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Canceled!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
        }

        private static void InfoP1()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Methods that will be called:");
                Console.WriteLine("    Initialize_SYS_MedicalEntity");
                Console.WriteLine("    Initialize_SYS_MedicalSpecialty");
                Console.WriteLine("    Initialize_SYS_Contracts");
                Console.WriteLine("    Initialize_SYS_Cid10");
                Console.WriteLine("    Initialize_SYS_MedicalProcedures");
                Console.WriteLine("    Create_CrmMg_Psiquiatria_DrHouse_Andre_Miguel_Thomas");
                Console.WriteLine("    CreateSecretary_Milena");
                Console.WriteLine("    SetupDoctor (for each doctor)");
                Console.WriteLine("    CreateFakePatients (for each doctor)");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptP1()
        {
            try
            {
                using (var db = this.CreateCerebelloEntities())
                {
                    if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");

                    Console.ForegroundColor = ConsoleColor.Gray;

                    // Initializing system tables
                    this.InitSysTables(db);

                    using (RandomContext.Create(this.rndOption))
                        OptionP1(db);

                    if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__reset__");

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.ConsoleWriteException(ex);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Partially done!");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void InfoAnvll()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Downloads and saves all leaflets from Anvisa official site.");
                Console.WriteLine("A JSON file is going to be saved with all data.");
                Console.WriteLine("This file is used to populate DB later.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptAnvll()
        {
            try
            {
                using (var db = this.CreateCerebelloEntities())
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("Saving to: {0}", new FileInfo("medicines.json").FullName);

                    // Downloading data from Anvisa official site.
                    var anvisaHelper = new AnvisaLeafletHelper();
                    var meds = anvisaHelper.DownloadAndCreateMedicinesJson();

                    Console.WriteLine("Total medicines: {0}", meds.Count);
                    Console.WriteLine("Saved to: {0}", new FileInfo("medicines.json").FullName);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.ConsoleWriteException(ex);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Partially done!");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void InfoQ()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Quits the program.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptQ()
        {
            // Dettaching previous DB if it was attached in this session.
            if (this.detachDbWhenDone)
            {
                bool ok = false;
                using (var db = this.CreateCerebelloEntities())
                    try
                    {
                        Firestarter.DetachLocalDatabase(db);
                        ok = true;
                    }
                    catch
                    {
                    }

                Console.WriteLine();
                if (ok)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("DB detached.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("DB NOT detached.");
                    ConsoleHelper.PressAnyKeyToContinue();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Bye!");
        }

        private static void InfoDb()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Changes the current database.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private static bool OptDb()
        {
            bool isToChooseDb;
            isToChooseDb = true;
            Console.WriteLine();

            return isToChooseDb;
        }

        private static void InfoRnd()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Sets a new seed to the random generator.");
                Console.WriteLine("If left empty, uses an unpredictable seed.");
                Console.Write("Default seed is ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(defaultSeed);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine('.');
                Console.WriteLine("A predictable seed will always produce the same set of results,");
                Console.WriteLine("suitable for unit tests, and an unpredictable seed will produce");
                Console.WriteLine("different sets of result each time it is run, being suitable");
                Console.WriteLine("for human interaction tests.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptRnd()
        {
            // Dettaching previous DB if it was attached in this session.
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Seed: ");
            var numText = Console.ReadLine();
            this.rndOption = null;
            int num;
            if (int.TryParse(numText, out num))
                this.rndOption = num;
        }

        private static void InfoBkc()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Creates a database backup,");
                Console.WriteLine("that can latter be restored");
                Console.WriteLine("using the 'bkr' command.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptBkc()
        {
            // Dettaching previous DB if it was attached in this session.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Create Backup");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Backup name: ");
            var ssName = Console.ReadLine();
            string fileName = null;

            try
            {
                using (var db = this.CreateCerebelloEntities())
                {
                    bool canCreate = !Firestarter.BackupExists(db, ssName)
                                     || ConsoleHelper.YesNo("Backup already exists. Would you like to replace it?");

                    if (this.isFuncBackupEnabled && canCreate)
                        fileName = Firestarter.CreateBackup(db, ssName);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.ConsoleWriteException(ex);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            if (fileName != null)
                Console.WriteLine("File name: {0}", fileName);

            Console.ForegroundColor = fileName != null ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(fileName != null ? "Done!" : "Backup not created.");

            Console.ForegroundColor = ConsoleColor.White;
            ConsoleHelper.PressAnyKeyToContinue();
        }

        private static void InfoBkr()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Restores a database backup,");
                Console.WriteLine("that was created using the");
                Console.WriteLine("'bkc' command.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptBkr()
        {
            // Dettaching previous DB if it was attached in this session.
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Restore Backup");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Backup name: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            var ssNameRestore = Console.ReadLine();
            string fileName = null;
            bool fileExists;
            try
            {
                using (var db = this.CreateCerebelloEntities())
                {
                    if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                    try
                    {
                        fileExists = Firestarter.BackupExists(db, ssNameRestore);
                        if (fileExists)
                            if (ConsoleHelper.YesNo("Restoring can destroy schema and data changes, are you sure?"))
                                fileName = Firestarter.RestoreBackup(db, ssNameRestore);
                    }
                    finally
                    {
                        if (this.isFuncBackupEnabled)
                            Console.WriteLine("Use 'undo' command to restore database to what it was before.");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.ConsoleWriteException(ex);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            if (fileName != null)
            {
                Console.WriteLine("File name: {0}", fileName);

                Console.ForegroundColor = fileExists ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(fileExists ? "Done!" : "File not found");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed!");
            }

            Console.ForegroundColor = ConsoleColor.White;
            ConsoleHelper.PressAnyKeyToContinue();
        }

        private static void InfoR()
        {
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine("Resets the current database, to the default working state.");
                Console.WriteLine("Work database is dropped, recreated, and populated with p1 option.");
                Console.WriteLine("Test database is dropped, recreated, and populated with all SYS tables.");
                Console.WriteLine();
                ConsoleHelper.PressAnyKeyToContinue();
            }
        }

        private void OptR()
        {
            {
                bool isTestDb = this.connName.ToUpper().Contains("TEST");

                bool isAzureDb = this.connName.ToUpper().Contains("AZURE");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("DB type: {0}", isTestDb ? "TEST" : "WORK");

                var resetDb = ConsoleHelper.YesNo("Reset can destroy schema and data changes, are you sure?");

                Console.ForegroundColor = ConsoleColor.Gray;

                // Doing what the user has told.
                using (var db = this.CreateCerebelloEntities())
                {
                    if (resetDb)
                    {
                        if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                        try
                        {
                            Console.WriteLine("DropAllTables");
                            Firestarter.DropAllTables(db);

                            Console.WriteLine("CreateDatabaseUsingScript");
                            this.CreateDatabaseUsingScript(db);

                            if (!isAzureDb)
                            {
                                // recreating the [NT AUTHORITY\NETWORK SERVICE] user
                                // so that we can debug using IIS, but only for the local databases,
                                // never for production (i.e. Azure)
                                Console.WriteLine(@"Creating user: [NT AUTHORITY\NETWORK SERVICE]");
                                Firestarter.RecreateUser(db, @"NT AUTHORITY\NETWORK SERVICE");
                                Console.WriteLine(@"Creating user: [IIS APPPOOL\ASP.NET V4.0 Integrated]");
                                Firestarter.RecreateUser(db, @"IIS APPPOOL\ASP.NET V4.0 Integrated");
                            }

                            this.InitSysTables(db);

                            if (!isTestDb)
                                using (RandomContext.Create(this.rndOption))
                                    OptionP1(db);

                            if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__reset__");
                        }
                        catch (Exception ex)
                        {
                            ConsoleHelper.ConsoleWriteException(ex);

                            if (this.isFuncBackupEnabled)
                                Console.WriteLine("Use 'undo' command to restore database to what it was before.");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Partially done!");
                            Console.ForegroundColor = ConsoleColor.White;

                            return;
                        }

                        Console.WriteLine("Use 'zero' command to restore database to a minimal state (only SYS data).");
                        Console.WriteLine("Use 'undo' command to restore database to what it was before.");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Done!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Canceled!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
        }

        private static void InfoEml()
        {
        }

        private void OptEml()
        {
            var t = new Thread(this.OptEmlThread);
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }

        private void OptEmlThread()
        {

            {
                try
                {
                    using (var db = this.CreateCerebelloEntities())
                    {
                        // collecting e-mails and objects
                        var emailData = db.Users.Include("Person")
                            .Where(u => u.IsOwner)
                            .Where(u => !u.Practice.AccountDisabled)
                            .Where(u => !u.Practice.UrlIdentifier.StartsWith("consultorioplus"))
                            .Where(u => !u.Practice.UrlIdentifier.StartsWith("consultoriodrhouse"))
                            .Where(u => u.UserName != "andrerpena")
                            .AsEnumerable()
                            .Select(u => new FormSendEmail.EmailData(db, u)).ToArray();

                        var form = new FormSendEmail(this.configData.FormSendEmail, emailData);
                        form.ShowDialog();
                        this.configData.FormSendEmail = form.ConfigData;

                        this.SaveConfigurations();
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.ConsoleWriteException(ex);
                }
                finally
                {
                }
            }
        }

        private FirestarterConfigData configData;
        private string configFilePath;

        private void SaveConfigurations()
        {
            // saving the fs.config file
            var ws = new XmlWriterSettings { NewLineHandling = NewLineHandling.Entitize };
            var ser = new XmlSerializer(typeof(FirestarterConfigData));
            using (var reader = XmlWriter.Create(this.configFilePath, ws))
            {
                ser.Serialize(reader, this.configData);
            }
        }

        private void LoadConfigurations()
        {
            // loading the fs.config file
            var data = new FirestarterConfigData { ConfigLocation = Path.Combine(".", "fs.config") };
            var ser = new XmlSerializer(typeof(FirestarterConfigData));
            FileInfo fileInfo = null;
            while (!string.IsNullOrWhiteSpace(data.ConfigLocation))
            {
                fileInfo = new FileInfo(data.ConfigLocation);
                if (File.Exists(fileInfo.FullName))
                {
                    using (var reader = XmlReader.Create(fileInfo.FullName))
                    {
                        data = (FirestarterConfigData)ser.Deserialize(reader);
                    }
                }
            }

            Debug.Assert(fileInfo != null, "fileInfo != null");
            this.configFilePath = fileInfo.FullName;
            this.configData = File.Exists(this.configFilePath) ? data : new FirestarterConfigData();
        }

        private void OptExec()
        {
            try
            {
                Console.Write("Command: ");
                using (var db = this.CreateCerebelloEntities())
                {
                    if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__undo__");
                    new Exec(db).ExecCommand(0, "", null, new Queue<string>());
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done!");
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                ConsoleHelper.ConsoleWriteException(ex);
            }

            ConsoleHelper.PressAnyKeyToContinue();
        }

        private class Exec
        {
            private readonly CerebelloEntities db;

            public Exec(CerebelloEntities db)
            {
                this.db = db;
            }

            private static string ReadCommand(Queue<string> commands)
            {
                bool empty = commands.Count == 0;
                if (empty)
                    foreach (var command in Console.ReadLine().Split(' '))
                        commands.Enqueue(command);

                string input = commands.Dequeue();
                var matchStr = Regex.Match(input, @"^\s*""");
                if (matchStr.Success)
                    while (!(matchStr = Regex.Match(input, @"^\s*""((\\""|.)*)""\s*$")).Success)
                        input += " " + commands.Dequeue();

                if (!empty)
                    Console.WriteLine(input);

                if (matchStr.Success)
                    input = matchStr.Groups[1].Value.Replace("\\\\", "\\").Replace("\\n", "\n")
                        .Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\0", "\0").Replace("\\v", "\v");

                return input;
            }

            /// <summary>
            /// Executes a command on this class using reflection.
            /// </summary>
            /// <param name="level">Level of indentation of the command.</param>
            /// <param name="prefix">Command prefix, so that if prefix is "New" and user input is "User", the command is "NewUser".</param>
            /// <param name="outType">Type that should be returned. Only used for numeric litterals.</param>
            /// <param name="commands">List of commands in the queue.</param>
            /// <returns></returns>
            public object ExecCommand(int level, string prefix, Type outType, Queue<string> commands)
            {
                Console.ForegroundColor = ConsoleColor.White;
                var input = ReadCommand(commands);

                if (input == "null")
                    return null;

                if (outType == typeof(string))
                    return input;

                if (outType != null && outType != typeof(string) && outType.IsPrimitive)
                    try
                    {
                        return Convert.ChangeType(input, outType);
                    }
                    catch
                    {
                    }

                var command = prefix + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input);
                var method = typeof(Exec).GetMethod(command, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                {
                    var listParam = new List<object>();
                    foreach (var eachParam in method.GetParameters())
                    {
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.Write("{0}{1} ", new string(' ', (level + 1) * 4), eachParam.ParameterType.Name);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write("{0}: ", eachParam.Name);
                        listParam.Add(this.ExecCommand(level + 1, eachParam.Name, eachParam.ParameterType, commands));
                    }

                    return method.Invoke(this, listParam.ToArray());
                }

                if (input == "")
                    throw new Exception("Invalid input.");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("{0}{1}: ", new string(' ', (level + 1) * 4), command);
                return this.ExecCommand(level + 1, command, outType, commands);
            }

            #region Commands called via reflection

            // ReSharper disable MemberCanBePrivate.Local
            // ReSharper disable UnusedMember.Local
            // ReSharper disable UnusedParameter.Local

            public void DelUser(User user)
            {
                if (user != null && user.Secretary != null)
                    this.db.DeleteObject(user.Secretary);
                if (user != null && user.Administrator != null)
                    this.db.DeleteObject(user.Administrator);
                if (user != null && user.Doctor != null)
                    this.db.DeleteObject(user.Doctor);
                if (user != null) this.db.DeleteObject(user);
                this.db.SaveChanges();
            }

            public void DelPractice(Practice practice)
            {
                if (practice != null) this.db.DeleteObject(practice);
                this.db.SaveChanges();
            }

            public void SetOwner(User user)
            {
                if (user != null)
                {
                    if (user.Practice != null)
                    {
                        if (user.Practice.Owner != null) user.Practice.Owner.IsOwner = false;
                        user.Practice.Owner = user;
                    }
                    user.IsOwner = true;
                }
                this.db.SaveChanges();
            }

            public void DelSecretary(Secretary secretary)
            {
                this.DelUser(secretary.Users.Single());
            }

            public void DelAdministrator(Administrator administrator)
            {
                this.DelUser(administrator.Users.Single());
            }

            public void DelDoctor(Doctor doctor)
            {
                this.DelUser(doctor.Users.Single());
            }

            public Secretary NewSecretary(Practice practice, string username, string password, string name)
            {
                var user = Firestarter.CreateUser(this.db, practice, username, password, name);
                user.Secretary = new Secretary { PracticeId = user.PracticeId };
                return user.Secretary;
            }

            public Secretary NewSecretaryTtMilena(Practice practice)
            {
                return Firestarter.CreateSecretary_Milena(this.db, practice);
            }

            public Secretary SecretaryMilena()
            {
                return this.db.Secretaries.First(x => x.Users.FirstOrDefault().UserName == "milena");
            }

            public Practice PracticeNew(string name, User user, string urlId)
            {
                return Firestarter.CreatePractice(this.db, name, user, urlId);
            }

            public Practice NewPractice(string name, User user, string urlId)
            {
                return Firestarter.CreatePractice(this.db, name, user, urlId);
            }

            public Practice PracticeFirst()
            {
                return this.db.Practices.FirstOrDefault();
            }

            public Practice PracticeDrHouse()
            {
                return this.db.Practices.Single(x => x.UrlIdentifier == "consultoriodrhouse");
            }

            public Practice PracticeByUrlId(string urlIdentifier)
            {
                return this.db.Practices.Single(x => x.UrlIdentifier == urlIdentifier);
            }

            public User UserAndre()
            {
                return this.db.Users.Single(x => x.UserName == "andrerpena");
            }

            public User UserMiguel()
            {
                return this.db.Users.Single(x => x.UserName == "masbicudo");
            }

            public User UserOwner(Practice practice)
            {
                return practice.Owner;
            }

            public Doctor DoctorById(int id)
            {
                return this.db.Doctors.Single(d => d.Id == id);
            }

            public Secretary SecretaryById(int id)
            {
                return this.db.Secretaries.Single(d => d.Id == id);
            }

            public User UserById(int id)
            {
                return this.db.Users.Single(d => d.Id == id);
            }

            public User UserNew(string username, string password, string name, string type)
            {
                var user = Firestarter.CreateUser(this.db, null, username, password, name);
                if (type.Contains("adm"))
                    user.Administrator = new Administrator { PracticeId = user.PracticeId };
                if (type.Contains("sec"))
                    user.Secretary = new Secretary { PracticeId = user.PracticeId };
                if (type.Contains("doc"))
                {
                    user.Doctor = new Doctor { PracticeId = user.PracticeId };
                    user.Doctor.HealthInsurances.Add(new HealthInsurance
                        {
                            PracticeId = user.PracticeId,
                            Name = "Particular",
                            IsActive = true,
                            IsParticular = true,
                        });
                }
                return user;
            }

            public Administrator AdministratorById(int id)
            {
                return this.db.Administrators.Single(d => d.Id == id);
            }

            public void Pwd(string password)
            {
                var passwordSalt = CipherHelper.GenerateSalt();
                var passwordHash = CipherHelper.Hash(password, passwordSalt);
                Console.WriteLine();
                Console.WriteLine(@"PasswordSalt = ""{0}"",", passwordSalt);
                Console.WriteLine(@"Password = ""{0}"", // pwd: '{1}'", passwordHash, password);
                Console.WriteLine();
            }

            // ReSharper restore UnusedParameter.Local
            // ReSharper restore UnusedMember.Local
            // ReSharper restore MemberCanBePrivate.Local

            #endregion
        }

        private static void OptionP1(CerebelloEntities db)
        {
            // Create practice, contract, doctors and other things
            Console.WriteLine("Create_CrmMg_Psiquiatria_DrHouse_Andre_Miguel_Thomas");
            var doctorsList = Firestarter.Create_CrmMg_Psiquiatria_DrHouse_Andre_Miguel_Thomas(db);

            // Create practice, contract, doctors and other things
            Console.WriteLine("CreateSecretary_Milena");
            Firestarter.CreateSecretary_Milena(db, doctorsList[0].Users.First().Practice);

            // Setup doctor schedule and document templates
            Console.WriteLine("SetupDoctor");
            using (var rc = RandomContext.Create())
                foreach (var doctor in doctorsList)
                    Firestarter.SetupDoctor(doctor, db, rc.Random.Next());

            // Create patients
            Console.WriteLine("CreateFakePatients");
            using (RandomContext.Create())
                foreach (var doctor in doctorsList)
                    Firestarter.CreateFakePatients(doctor, db);

            // Create appointments
            Console.WriteLine("CreateFakeAppointments");
            using (var rc = RandomContext.Create())
                foreach (var doctor in doctorsList)
                    Firestarter.CreateFakeAppointments(db, doctor, rc.Random.Next());
        }

        private void InitSysTables(CerebelloEntities db)
        {
            Console.WriteLine("Initialize_SYS_MedicalEntity");
            Firestarter.Initialize_SYS_MedicalEntity(db);

            Console.WriteLine("Initialize_SYS_MedicalSpecialty");
            Firestarter.Initialize_SYS_MedicalSpecialty(db);

            Console.WriteLine("Initialize_SYS_Contracts");
            Firestarter.Initialize_SYS_Contracts(db);

            Console.WriteLine("Initialize_SYS_Cid10");
            Firestarter.Initialize_SYS_Cid10(
                db,
                progress: ConsoleHelper.ConsoleWriteProgressIntInt);

            Console.WriteLine("Initialize_SYS_MedicalProcedures");

            var cbhpmFilePath = Path.Combine(this.rootCerebelloPath, @"DB\cbhpm_2010.txt");
            if (!File.Exists(cbhpmFilePath))
            {
                cbhpmFilePath = "cbhpm_2010.txt";
                if (!File.Exists(cbhpmFilePath))
                    throw new Exception("Could not find file cbhpm_2010.txt");
            }

            Firestarter.Initialize_SYS_MedicalProcedures(
                db,
                cbhpmFilePath,
                progress: ConsoleHelper.ConsoleWriteProgressIntInt);

            Console.WriteLine("SaveLeafletsInMedicinesJsonToDb");
            var anvisaHelper = new AnvisaLeafletHelper();
            anvisaHelper.SaveLeafletsInMedicinesJsonToDb(
                db,
                progress: ConsoleHelper.ConsoleWriteProgressIntInt);

            // Creating a minimal DB backup called __zero__.
            if (this.isFuncBackupEnabled) Firestarter.CreateBackup(db, "__zero__");
        }

        private void CreateDatabaseUsingScript(CerebelloEntities db)
        {
            // ToDo: figure out a way to remove this.. we should have a common path or something

            string scriptText;
            try
            {
                var path = Path.Combine(this.rootCerebelloPath, @"DB\Scripts");
                scriptText = File.ReadAllText(Path.Combine(path, "script.sql"));
            }
            catch
            {
                scriptText = File.ReadAllText("script.sql");
            }

            var scriptText2 = SqlHelper.SetScriptColumnsCollation(scriptText, "Latin1_General_CI_AI");

            // We don't want to create users in this script, so we remove them.
            var script = new SqlScript();
            script.Load(scriptText2);
            script.Items.RemoveAll(x => x.Kind == SqlKinds.User);
            var scriptText3 = script.ToString();

            // Creating tables.
            Firestarter.ExecuteScript(db, scriptText3);
        }
    }

    // ReSharper restore LocalizableElement
}
