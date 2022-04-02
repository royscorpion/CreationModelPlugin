﻿using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            #region Исходные данные для построения. Все размеры - [мм].

            double wallLength = 10000; // длина стеновой коробки
            double wallWidth = 5000; // ширина стеновой коробки
            double windowSillHeight = 500; // высота подоконника
            string wallBaseLevelName = "Уровень 1"; // наименование базового уровня стен
            string wallTopConstraintLevelName = "Уровень 2"; // наименование уровня ограничивающего высоту стен сверху

            #endregion

            #region Построение

            //Создание стеновой коробки
            List<Wall> walls = CreateWallBox(doc, wallLength, wallWidth, wallBaseLevelName, wallTopConstraintLevelName, false);

            //Создание двери
            AddDoor(doc, walls[0]);

            //Создание окон
            AddWindows(doc, walls.GetRange(1, 3), windowSillHeight);

            AddRoof(doc, wallTopConstraintLevelName, walls);

            #endregion

            return Result.Succeeded;
        }




        #region Методы построения

        //Метод создания крыши по контуру стен
        private void AddRoof(Document doc, string levelName, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double dt = walls[0].Width/2;
            //double dt = wallWidth / 2;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Level level = GetLevelByName(doc, levelName);

            Transaction transaction = new Transaction(doc, "Create roof");
            transaction.Start();

            Application application = doc.Application;
            CurveArray footprint = application.Create.NewCurveArray();
            for (int i = 0; i < walls.Count; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve;
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                footprint.Append(line);
            }
            ModelCurveArray footprintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out footprintToModelCurveMapping);
            foreach (ModelCurve mc in footprintToModelCurveMapping)
            {
                footprintRoof.set_DefinesSlope(mc, true);
                footprintRoof.set_SlopeAngle(mc, 0.5);
            }
            transaction.Commit();

        }


        //Метод добавления окон предопределенного типа в стены из списка, с назначением высоты подоконника
        private void AddWindows(Document doc, List<Wall> walls, double sillHeight)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Windows)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 1830 мм"))
                 .Where(x => x.FamilyName.Equals("Фиксированные"))
                 .FirstOrDefault();

            sillHeight = UnitUtils.ConvertToInternalUnits(sillHeight, UnitTypeId.Millimeters);

            Transaction transaction = new Transaction(doc, "Create windows");
            transaction.Start();
            if (!windowType.IsActive)
                windowType.Activate();
            for (int i = 0; i < walls.Count; i++)
            {
                Level level = GetLevelById(doc, walls[i].LevelId);
                LocationCurve hostCurve = walls[i].Location as LocationCurve;
                XYZ point1 = hostCurve.Curve.GetEndPoint(0);
                XYZ point2 = hostCurve.Curve.GetEndPoint(1);
                XYZ point = (point1 + point2) / 2;

                FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, walls[i], level, StructuralType.NonStructural);
                window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(sillHeight);

            }
            transaction.Commit();
        }


        //Метод добавления двери предопределенного типа в указанную стену
        private void AddDoor(Document doc, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Doors)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 2134 мм"))
                 .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                 .FirstOrDefault();


            Level level = GetLevelById(doc, wall.LevelId);
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            Transaction transaction = new Transaction(doc, "Create door");
            transaction.Start();

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level, StructuralType.NonStructural);

            transaction.Commit();
        }



        //Метод построения стеновой коробки (прямоугольник на плане)
        public List<Wall> CreateWallBox(Document doc, double length, double width, string levelName, string topConstraintLevel, bool structural)
        {
            Level level1 = GetLevelByName(doc, levelName);
            Level level2 = GetLevelByName(doc, topConstraintLevel);

            List<XYZ> points = GetRectangleCornersBySizes(length, width);

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Create walls");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, structural);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }

            transaction.Commit();
            return walls;

        }

        //Метод получения уровня по его наименованию
        public Level GetLevelByName(Document doc, string name)
        {
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            Level level = levels
                .Where(x => x.Name.Equals(name))
                .FirstOrDefault();
            return level;
        }

        public Level GetLevelById(Document doc, ElementId id)
        {
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            Level level = levels
                .Where(x => x.Id.Equals(id))
                .FirstOrDefault();
            return level;
        }

        ///Метод получения точек углов прямоугольника (относительно центра прямоугольника) по его длинам его сторон, заданных в миллиметрах,
        ///с конвертацией во внутренние единицы измерения
        public List<XYZ> GetRectangleCornersBySizes(double a, double b)
        {
            double dx = UnitUtils.ConvertToInternalUnits(a / 2, UnitTypeId.Millimeters);
            double dy = UnitUtils.ConvertToInternalUnits(b / 2, UnitTypeId.Millimeters);

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            return points;
        }

        #endregion
    }
}
