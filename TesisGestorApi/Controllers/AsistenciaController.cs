using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TesisGestorApi.Controllers
{
    public class AsistenciaController : Controller
    {
        // GET: AsistenciaController
        public ActionResult Index()
        {
            return View();
        }

        // GET: AsistenciaController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: AsistenciaController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: AsistenciaController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: AsistenciaController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: AsistenciaController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: AsistenciaController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: AsistenciaController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
