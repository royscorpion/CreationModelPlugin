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

            #region Исходные данные для построения

            double wallLength = 10000;
            double wallWidth = 5000;
            string wallBaseLevelName = "Уровень 1";
            string wallTopConstraintLevelName = "Уровень 2";

            #endregion

            #region Построение

            //Создание стеновой коробки
            List<Wall> walls = CreateWallBox(doc, wallLength, wallWidth, wallBaseLevelName, wallTopConstraintLevelName, false);

            //Создание двери
            AddDoor(doc, walls[0]);
            //AddDoor(doc, wallBaseLevelName, walls[0]);


            #endregion

            return Result.Succeeded;
        }


        #region Методы построения

        //Метод добавления двери предопределенного типа в указанную стену на указанном уровне
        private void AddDoor(Document doc, Wall wall)
        //private void AddDoor(Document doc, string levelName, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                 .OfClass(typeof(FamilySymbol))
                 .OfCategory(BuiltInCategory.OST_Doors)
                 .OfType<FamilySymbol>()
                 .Where(x => x.Name.Equals("0915 x 2134 мм"))
                 .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                 .FirstOrDefault();

            
            Level level = GetLevelById(doc, wall.LevelId);
            //Level level = GetLevelByName(doc, levelName);
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
