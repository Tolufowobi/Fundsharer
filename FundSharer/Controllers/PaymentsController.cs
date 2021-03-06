﻿using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using FundSharer.Models;
using Microsoft.AspNet.Identity;
using System.Web;
using FundSharer.DataServices;
using System.Collections.Generic;

namespace FundShare.Controllers
{
    [Authorize]
    public class PaymentsController : Controller
    {
        private ApplicationDbContext db = DbAccessHandler.DbContext;

        // GET: Payments
        public ActionResult Index()
        {
            return View(db.Payments.ToList());
        }

        // GET: Payments/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        // GET: Payments/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Payments/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Id,Amount,CreationDate,Confirmed")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                db.Payments.Add(payment);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(payment);
        }

        // GET: Payments/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        // POST: Payments/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Amount,CreationDate,Confirmed")] Payment payment)
        {
            if (ModelState.IsValid)
            {
                db.Entry(payment).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(payment);
        }

        // GET: Payments/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = db.Payments.Find(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Payment payment = db.Payments.Find(id);
            db.Payments.Remove(payment);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        public ActionResult UploadPaymentInfo(string Id)
        {
            Donation donation = DonationServices.GetDonationById(Id);
                if (donation != null)
                {
                    DonationDetails details = new DonationDetails
                    {
                        DonationId = donation.Id,
                        DonorBankName = donation.Donor.Bank,
                        DonorAccountNumber = donation.Donor.AccountNumber,
                        DonorFullName = donation.Donor.AccountTitle,
                        RecipientAccountNumber = donation.Ticket.TicketHolder.AccountNumber,
                        RecipientBankName = donation.Ticket.TicketHolder.Bank,
                        RecipientFullName = donation.Ticket.TicketHolder.AccountTitle
                    };
                    return View(details);
                }
            
            else
            {
                return HttpNotFound("We could not find the information you are looking for.");
            }

        }

        [HttpPost]
        public ActionResult UploadPaymentInfo(HttpPostedFileBase image)
        {
            if (image != null)
            {
                using (var db = new ApplicationDbContext())
                {
                    var userId = User.Identity.GetUserId();
                    var AppUser = db.Users.Find(userId);
                    BankAccount UserBankAccount = BankAccountServices.GetUserBankAccount(AppUser);
                    var outgoingdonations = (from d in db.Donations where d.DonorId == UserBankAccount.Id && d.IsOpen == false select d);
                    Donation donation = null;
                    if (outgoingdonations.Count() == 1)
                    {
                        donation = outgoingdonations.First();
                    }
                    Payment NewPay = new Payment
                    {
                        DonationPack = donation,
                        CreationDate = DateTime.Now,
                        Confirmed = false,
                    };
                    db.Payments.Add(NewPay);
                    POPImage PopImg = new POPImage();
                    PopImg.Payment = NewPay;
                    PopImg.Image = new byte[image.ContentLength];
                    image.InputStream.Read(PopImg.Image, 0, image.ContentLength);
                    db.POPImages.Add(PopImg);
                    db.SaveChanges();
                }
                return RedirectToAction("Welcome", "Home");
            }
            else
            {
                return HttpNotFound("Something went wrong");
            }

           
        }

        //Get
        public ActionResult ConfirmPayment(String Id)
        {
            Payment pay = PaymentServices.GetPaymentById(Id);
            if(pay.Confirmed == false)
            {
                POPImage img = PopImageServices.GetPaymentPopImage(pay);
                PaymentDetails details = new PaymentDetails
                {
                    PaymentId = pay.Id,
                    Amount = pay.Amount,
                    DonorName = pay.DonationPack.Donor.BankAccountOwner.FullName,
                    RecipientAccountNumber = pay.DonationPack.Ticket.TicketHolder.AccountNumber,
                    Date = pay.CreationDate.ToShortDateString(),
                    POPimage = img.Image
                };
                    return PartialView("_ConfirmPayment", details);
            }
            else
            {
                return RedirectToAction("Welcome", "Home");
            }
        }

        //POST
        [HttpPost]
        [ActionName("ConfirmPayment")]
        public ActionResult PaymentConfirmed(string Id)
        {
            if (Id != null)
            {
                using (var db = new ApplicationDbContext())
                {
                    var pay = db.Payments.Find(Id);// get the payment object associated with the supplied id 
                    if (pay.Confirmed == false)// ensure that the payment hasnt been previously confirmed.
                    {
                        pay.Confirmed = true; // set its confirmation status to true
                        pay.DonationPack.IsOpen = true; // open its donation package
                        pay.DonationPack.Donor.IsReceiver = true; // set the donors receiver status to true
                        var newticket = new WaitingTicket
                        {
                            TicketHolder = pay.DonationPack.Donor,
                            TicketHolderId = pay.DonationPack.Donor.Id,
                            EntryDate = DateTime.Now,
                        }; // Create a ticket for the donor
                        db.WaitingList.Add(newticket);// add the ticket to record
                        db.SaveChanges();// save current changes
                        if (pay.DonationPack.Ticket.Donations.Count > 1 && pay.DonationPack.Ticket.Donations.Where(m => m.IsOpen == true).Count() == 2)
                        {//If the donation ticket has up to 2 donations and both hav been opened, change the status of the recipient.
                            pay.DonationPack.Ticket.TicketHolder.IsReceiver = false;
                            pay.DonationPack.Ticket.IsValid = false;
                            db.SaveChanges();// save current changes
                        }
                    }
                        return Json("Done");
                }

            }
            return HttpNotFound("Record could not be found");
        }

        [Authorize(Roles="Administrator")]
        public PartialViewResult Payments()
        {
            List<PaymentDetails> Payments = new List<PaymentDetails>();
            using (var db = new ApplicationDbContext())
            {
                (from p in db.Payments select p).ToList().ForEach( py => Payments.Add(new PaymentDetails
                { PaymentId = py.Id,
                DonorName = py.DonationPack.Donor.AccountTitle,
                RecipientName = py.DonationPack.Ticket.TicketHolder.AccountTitle,
                Amount = py.Amount,
                Status = py.Confirmed,
                Date = py.CreationDate.ToShortDateString(), POPimage = (from i in db.POPImages where i.PaymentId == py.Id select i).FirstOrDefault().Image}));
            }

                return PartialView("_Payments", Payments);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
