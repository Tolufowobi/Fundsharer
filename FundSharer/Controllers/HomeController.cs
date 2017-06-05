﻿using FundSharer.DataServices;
using FundSharer.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FundSharer.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult HomePage()
        {
            ApplicationUser AppUser;
            // Get data
            var UserId = User.Identity.GetUserId();
            AppUser = DbAccessHandler.DbContext.Users.Find(UserId);
            
            var UserBankAccount = BankAccountServices.GetUserBankAccount(AppUser);
            var IncomingDonations = DonationServices.GetIncomingAccountDonations(UserBankAccount);
            List<Payment> IncomingPayments = new List<Payment>();
            foreach (Donation d in IncomingDonations)
            {
                Payment p = PaymentServices.GetPaymentForDonation(d);
                if (PaymentServices.IsNotNull(p))
                {
                    IncomingPayments.Add(p);
                }
            }

            var OutgoingDonations = DonationServices.GetOutgoingAccountDonations(UserBankAccount);
            List<Payment> OutgoingPayments = new List<Payment>();
            foreach (Donation d in OutgoingDonations)
            {
                Payment p1 = PaymentServices.GetPaymentForDonation(d);
                if(PaymentServices.IsNotNull(p1))
                {
                    OutgoingPayments.Add(p1);
                }
            }
            

            List<Donation> _pendingIncomingDonations = new List<Donation>(2);
            List<Payment> _pendingpayments = new List<Payment>(2);

            Donation PendingOutgoingDonation = null;
            Payment PendingOutgoingPayment = null;

            if (UserBankAccount.IsReciever == false)
            {
                if (OutgoingDonations.Where(m => m.IsOpen == false).Count() > 0)
                {
                    PendingOutgoingDonation = OutgoingDonations.Where(m => m.IsOpen == false).First();
                }

                if (OutgoingPayments.Where(m => m.Confirmed = false).Count() > 0)
                {
                    PendingOutgoingPayment = OutgoingPayments.Where(m => m.Confirmed = false).First();
                }

            }
            else
            {
                _pendingIncomingDonations = IncomingDonations.Where(m => m.IsOpen == false).ToList();
                _pendingpayments = IncomingPayments.Where(m => m.Confirmed == false).ToList();
            }

            //Get Users personal Information
            var FirstName = AppUser.FirstName;
            var LastName = AppUser.LastName;
            var PhoneNumber = AppUser.PhoneNumber;
            if (AppUser.PhoneNumberConfirmed == false)
            {
                PhoneNumber = PhoneNumber + " (Unconfirmed)";
            }
            var Emailaddress = AppUser.Email;

            //Add objects to the data dictionary

            //Personal Information
            ViewBag.FirstName = FirstName;
            ViewBag.LastName = LastName;
            ViewBag.PhoneNumber = PhoneNumber;
            ViewBag.EmailAddress = Emailaddress;

            //Account Information
            ViewBag.AccountDetails = UserBankAccount;

            //Outgoing Payments Information
            ViewBag.OutgoingPayments = OutgoingPayments.Where(m => m.Confirmed == true).ToList();

            //Incoming Payments Information
            ViewBag.IncomingPayments = IncomingPayments.Where(m => m.Confirmed == true).ToList();

            //Pending Donors Information
            ViewBag.PendingIncomingDonations = _pendingIncomingDonations;

            //Pending Payments Information
            ViewBag.PendingPayments = _pendingpayments;

            //Pending Outgoing Donation View
            ViewBag.PendingOutgoingDonation = PendingOutgoingDonation;

            //Pending Outgoing Payment Information
            ViewBag.PendingOutgoingPayment = PendingOutgoingPayment;
            
            return View();
        }

        public ActionResult FindAMatch()
        {
            var UserId = User.Identity.GetUserId();
            ApplicationUser AppUser;
            using (var db = new ApplicationDbContext())
            {
                AppUser = db.Users.Find(UserId);
                BankAccount donor = (from ba in db.BankAccounts where ba.OwnerId == AppUser.Id select ba).FirstOrDefault();
                WaitingTicket ticket = (from t in db.WaitingList where t.Donations.Count < 2 select t).FirstOrDefault();
                if (ticket != null)
                {
                    Donation NewDonation = new Donation
                    {
                        Donor = donor,
                        DonorId = donor.Id,
                        IsOpen = false,
                        Ticket = ticket,
                        TicketId = ticket.Id,
                        CreationDate = DateTime.Now
                    };
                    db.Donations.Add(NewDonation);
                    db.SaveChanges();
                    ViewBag.Donation = NewDonation;
                    return PartialView("_FindAMatch");
                }
                else { return HttpNotFound(); }
            }
          
            
        }


    }

}
