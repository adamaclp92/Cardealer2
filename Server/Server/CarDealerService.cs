using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using static Server.CarDealing;
using Server;

namespace Server
{
    public class CarDealerService  : CarDealingBase
    {
        private static MongoClient mongoClient = new MongoClient("mongodb://localhost:27017");
        private static IMongoDatabase mongoDatabase = mongoClient.GetDatabase("cardealer");

        private static IMongoCollection<BsonDocument> balance = mongoDatabase.GetCollection<BsonDocument>("balance");
        private static IMongoCollection<BsonDocument> users = mongoDatabase.GetCollection<BsonDocument>("users");
        private static IMongoCollection<BsonDocument> cars = mongoDatabase.GetCollection<BsonDocument>("cars");

        static List<string> sessions = new List<string>();

        //Autók kilistázása
        public override async Task ListCars(ListCarsRequest request, IServerStreamWriter<ListCarsResponse> responseStream, ServerCallContext context)
        {
            if (sessions.Contains(request.Uid))
            {
                var filter = new FilterDefinitionBuilder<BsonDocument>().Empty;

                var result = cars.Find(filter);

                foreach (var item in result.ToList())
                {
                    await responseStream.WriteAsync(new ListCarsResponse()
                    {
                        Car = new Car()
                        {
                            Numberplate = item.GetValue("numberplate").AsString,
                            Brand = item.GetValue("brand").AsString,
                            Vintage = item.GetValue("vintage").ToInt32(),
                            Boughtprice = item.GetValue("boughtprice").ToInt32(),
                            Currentvalue = item.GetValue("currentvalue").ToInt32()
                        }
                    });
                }
            }
        }

        //Egyenleg mutatása
        public override Task<BalanceResponse> Balance(BalanceRequest request, ServerCallContext context)
        {
            if (sessions.Contains(request.Uid))
            {
                var filter = new FilterDefinitionBuilder<BsonDocument>().Empty;

                var result = balance.Find(filter);

                return Task.FromResult(new BalanceResponse() { Balance = result.ToList().First().GetValue("balance").ToInt32() });
            }
            else return Task.FromResult(new BalanceResponse() { Balance = 0 });
        }

        //Bejelentkezés
        public override Task<Session_Id> Login(User request, ServerCallContext context)
        {
            string id = "";
            if (request.Username == "" || request.Password == "")
            {
                return Task.FromResult(new Session_Id
                {
                    Id = id,
                    Message = "Nincs kitöltve a felhasználónév/jelszó!"
                });
            }
            else
            {
                var username = request.Username;
                var password = request.Password;

                var filter1 = new FilterDefinitionBuilder<BsonDocument>().Eq("username", username);
                var filter2 = new FilterDefinitionBuilder<BsonDocument>().Eq("password", password);

                var result1 = users.Find(filter1).FirstOrDefault();
                var result2 = users.Find(filter2).FirstOrDefault();

                if (result1 == null || result2 == null || result1 != result2)
                {
                    return Task.FromResult(new Session_Id
                    {
                        Id = id,
                        Message = "Rossz felhasználónév/jelszó!"
                    });
                }
                else
                {
                    id = Guid.NewGuid().ToString();
                    sessions.Add(id);
                    return Task.FromResult(new Session_Id { Id = id, Message = "Sikeres bejelentkezés!" });
                }
            }

        }

        //Kijelentkezés
        public override Task<Result> Logout(Session_Id request, ServerCallContext context)
        {
            Result result = new Result();
            string id = request.Id;
            if (sessions.Contains(id))
            {
                result.Success = "Sikeres kijelentkezés!";
                sessions.Remove(id);
            }
            else
                result.Success = "Nem volt bejelentkezve!";
            return Task.FromResult(new Result { Success = result.Success });
        }

        //Aktuálisan kijelölt autó
        public override async Task<ActualCarResponse> ActualCar(ActualCarRequest request, ServerCallContext context)
        {
            var numberplate = request.Numberplate;

            var filter = new FilterDefinitionBuilder<BsonDocument>().Eq("numberplate", numberplate);

            var result = cars.Find(filter).FirstOrDefault();

            Car car = new Car()
            {
                Numberplate = result.GetValue("numberplate").AsString,
                Brand = result.GetValue("brand").AsString,
                Vintage = result.GetValue("vintage").AsInt32,
                Boughtprice = result.GetValue("boughtprice").AsInt32,
                Currentvalue = result.GetValue("currentvalue").AsInt32

            };

            return new ActualCarResponse() { Car = car };
        }

        //Autó vásárlása
        public override Task<PurchaseCarResponse> PurchaseCar(PurchaseCarRequest request, ServerCallContext context)
        {
            if (!sessions.Contains(request.Uid))
            {
                return Task.FromResult(new PurchaseCarResponse() { Message = "Előbb be kell jelentkezned!" });
            }
            else
            {
                var numberplate = request.Car.Numberplate;

                var filter = new FilterDefinitionBuilder<BsonDocument>().Eq("numberplate", numberplate);

                var result = cars.Find(filter).FirstOrDefault();

                if (result != null)
                    return Task.FromResult(new PurchaseCarResponse() { Message = "Ilyen rendszámmal már szerepel autó az adatbázisban!" });
                else
                {
                    if (request.Car.Numberplate == "" || request.Car.Brand == "" || request.Car.Vintage <= 0
                        || request.Car.Boughtprice <= 0 || request.Car.Currentvalue <= 0)

                        return Task.FromResult(new PurchaseCarResponse() { Message = "Nincs minden adat megfelelően kitöltve!" });
                    else
                    {
                        var car = request.Car;

                        BsonDocument doc = new BsonDocument("numberplate", car.Numberplate)
                                                            .Add("brand", car.Brand)
                                                            .Add("vintage", car.Vintage)
                                                            .Add("boughtprice", car.Boughtprice)
                                                            .Add("currentvalue", car.Currentvalue);

                        cars.InsertOne(doc);

                        var balanceFilter = new FilterDefinitionBuilder<BsonDocument>().Empty;
                        var balanceResult = balance.Find(balanceFilter);
                        int actualbalance = balanceResult.ToList().First().GetValue("balance").ToInt32();
                        int balanceSub = request.Car.Boughtprice;
                        int balanceChange = actualbalance - balanceSub;

                        var update = Builders<BsonDocument>.Update.Set("balance", balanceChange);
                        balance.UpdateOne(balanceFilter, update);

                        return Task.FromResult(new PurchaseCarResponse() { Car = car, Message = "Sikeres autóvásárlás!" });
                    }
                }
            }
        }

        //Autó eladása
        public override Task<SellCarResponse> SellCar(SellCarRequest request, ServerCallContext context)
        {
            if (request.Numberplate != "")
            {
                var numberplate = request.Numberplate;

                var numberPlateFilter = new FilterDefinitionBuilder<BsonDocument>().Eq("numberplate", numberplate);

                var selectedCar = cars.Find(numberPlateFilter).FirstOrDefault();

                Car car = new Car()
                {
                    Currentvalue = selectedCar.GetValue("currentvalue").AsInt32

                };

                var balanceFilter = new FilterDefinitionBuilder<BsonDocument>().Empty;

                var actualBalanceFinder = balance.Find(balanceFilter);
                int actualBalance = actualBalanceFinder.ToList().First().GetValue("balance").ToInt32();
                int balanceSub = car.Currentvalue;
                int balanceChange = actualBalance + balanceSub;

                var update = Builders<BsonDocument>.Update.Set("balance", balanceChange);
                balance.UpdateOne(balanceFilter, update);
                cars.DeleteOne(numberPlateFilter);
                return Task.FromResult(new SellCarResponse { Message = "Autó eladva!" });

            }
            else
                return Task.FromResult(new SellCarResponse { Message = "Nincs elem kijelölve!" });
        }

        //Autó javítása
        public override Task<RepairCarResponse> RepairCar(RepairCarRequest request, ServerCallContext context)
        {
            if (request.Numberplate != "")
            {
                var numberplate = request.Numberplate;

                var numberPlateFilter = new FilterDefinitionBuilder<BsonDocument>().Eq("numberplate", numberplate);

                var selectedCar = cars.Find(numberPlateFilter).FirstOrDefault();
                Car car = new Car()
                {
                    Numberplate = selectedCar.GetValue("numberplate").AsString,
                    Brand = selectedCar.GetValue("brand").AsString,
                    Vintage = selectedCar.GetValue("vintage").AsInt32,
                    Boughtprice = selectedCar.GetValue("boughtprice").AsInt32,
                    Currentvalue = selectedCar.GetValue("currentvalue").AsInt32

                };

                var balanceFilter = new FilterDefinitionBuilder<BsonDocument>().Empty;

                var actualBalanceFinder = balance.Find(balanceFilter);
                int actualBalance = actualBalanceFinder.ToList().First().GetValue("balance").ToInt32();
                int balanceSub = Convert.ToInt32(car.Boughtprice * 0.1);
                int balanceChange = actualBalance - balanceSub;

                var updateBalance = Builders<BsonDocument>.Update.Set("balance", balanceChange);
                balance.UpdateOne(balanceFilter, updateBalance);


                int carCurrentChange = Convert.ToInt32(car.Currentvalue * 1.3);
                var updateCarCurrent = Builders<BsonDocument>.Update.Set("currentvalue", carCurrentChange);
                cars.UpdateOne(numberPlateFilter, updateCarCurrent);
                return Task.FromResult(new RepairCarResponse { Message = "Autó javítva!" });

            }
            else
                return Task.FromResult(new RepairCarResponse { Message = "Nincs elem kijelölve!" });

        }
    }
}
