﻿using System;
using System.Collections.Generic;
using System.Linq;
using Qwack.Core.Basic;
using Qwack.Dates;

namespace Qwack.Core.Instruments.Funding
{
    public class GenericSwapLeg
    {
        public GenericSwapLeg(DateTime startDate, DateTime endDate, Calendar calendars, Currency currency, Frequency resetFrequency, DayCountBasis dayBasis)
        {
            EffectiveDate = startDate;
            TerminationDate = new TenorDateAbsolute(endDate);
            ResetFrequency = resetFrequency;
            Currency = currency;
            SetAllCalendars(calendars);
            AccrualDCB = dayBasis;
        }

        public GenericSwapLeg(DateTime startDate, string tenor, Calendar calendars, Currency currency, Frequency resetFrequency, DayCountBasis dayBasis)
        {
            EffectiveDate = startDate;
            TerminationDate = new TenorDateRelative(new Frequency(tenor));
            ResetFrequency = resetFrequency;
            Currency = currency;
            SetAllCalendars(calendars);
            AccrualDCB = dayBasis;
        }

        private void SetAllCalendars(Calendar calendars)
        {
            FixingCalendar = calendars;
            AccrualCalendar = calendars;
            ResetCalendar = calendars;
            PaymentCalendar = calendars;
        }

        public Currency Currency { get; set; }
        public DateTime EffectiveDate { get; set; }
        public ITenorDate TerminationDate { get; set; }
        public Calendar FixingCalendar { get; set; }
        public Calendar ResetCalendar { get; set; }
        public Calendar AccrualCalendar { get; set; }
        public Calendar PaymentCalendar { get; set; }
        public RollType ResetRollType { get; set; } = RollType.ModFollowing;
        public RollType PaymentRollType { get; set; } = RollType.Following;
        public RollType FixingRollType { get; set; } = RollType.Previous;
        public string RollDay { get; set; } = "Termination";
        public StubType StubType { get; set; } = StubType.ShortFront;
        public Frequency ResetFrequency { get; set; }
        public Frequency FixingOffset { get; set; } = new Frequency("2b");
        public Frequency ForecastTenor { get; set; }
        public SwapLegType LegType { get; set; }
        public Frequency PaymentOffset { get; set; } = new Frequency("0b");
        public OffsetRelativeToType PaymentOffsetRelativeTo { get; set; } = OffsetRelativeToType.PeriodEnd;
        public decimal FixedRateOrMargin { get; set; }
        public decimal Nominal { get; set; } = 1e6M;
        public DayCountBasis AccrualDCB { get; set; }
        public FraDiscountingType FraDiscounting { get; set; }
        public AverageType AveragingType { get; set; }
        public ExchangeType NotionalExchange { get; set; }
        public SwapPayReceiveType Direction { get; set; }

        public CashFlowSchedule GenerateSchedule()
        {
            var startDate = EffectiveDate;
            var endDate = TerminationDate.Date(startDate, ResetRollType, ResetCalendar);
            var f = new CashFlowSchedule();
            var lf = new List<CashFlow>();

            if ((NotionalExchange == ExchangeType.FrontOnly) || (NotionalExchange == ExchangeType.Both))
                lf.Add(new CashFlow
                {
                    Notional = (double)Nominal * (Direction == SwapPayReceiveType.Payer ? -1.0 : 1.0),
                    Fv = (double)Nominal * (Direction == SwapPayReceiveType.Payer ? -1.0 : 1.0),
                    SettleDate = startDate,
                    DiscountFactor = 1.0,
                    FlowType = FlowType.FixedAmount
                });

            //need to handle stub types and roll day types
            switch (StubType)
            {
                case StubType.ShortFront:
                case StubType.LongFront:
                    {
                        var currentReset = GetNextResetDate(endDate, false);
                        while (GetNextResetDate(currentReset, false) >= startDate)
                        {
                            var q = new CashFlow();
                            q.ResetDateStart = currentReset;
                            q.AccrualPeriodStart = currentReset;
                            q.FixingDateStart = currentReset.SubtractPeriod(FixingRollType, FixingCalendar, FixingOffset);
                            q.AccrualPeriodEnd = currentReset.AddPeriod(ResetRollType, ResetCalendar, ResetFrequency);
                            q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);
                            // Q.Currency = _currency;
                            q.DiscountFactor = (LegType != SwapLegType.FixedNoAccrual) && (LegType != SwapLegType.FloatNoAccrual) ?
                                 q.AccrualPeriodStart.CalculateYearFraction(q.AccrualPeriodEnd, AccrualDCB) :
                                 1.0; q.Notional = (double)Nominal;
                            q.Fv = LegType == SwapLegType.Fixed ?
                                (double)Nominal * q.DiscountFactor * (double)FixedRateOrMargin :
                                0;
                            q.FixedRateOrMargin = (double)FixedRateOrMargin;
                            q.FlowType = LegType == SwapLegType.Fixed ? FlowType.FixedRate : FlowType.FloatRate;
                            q.Notional = (double)Nominal;
                            lf.Add(q);
                            currentReset = GetNextResetDate(currentReset, false);
                        }

                        if ((lf.Count == 0) || (lf.Last().AccrualPeriodStart != startDate))
                            if (StubType == StubType.LongFront)
                            {
                                var q = lf.Last();
                                q.ResetDateStart = startDate;
                                q.AccrualPeriodStart = startDate;
                                q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                    q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                    q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);

                            }
                            else
                            {
                                var q = new CashFlow();
                                q.AccrualPeriodStart = startDate;
                                q.FixingDateStart = startDate.SubtractPeriod(FixingRollType, FixingCalendar, FixingOffset);
                                q.AccrualPeriodEnd = lf.Count > 0 ? lf.Last().AccrualPeriodStart : endDate;
                                q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                    q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                    q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);
                                //Q.Currency = CCY;
                                q.DiscountFactor = (LegType != SwapLegType.FixedNoAccrual) && (LegType != SwapLegType.FloatNoAccrual) ?
                                    q.AccrualPeriodStart.CalculateYearFraction(q.AccrualPeriodEnd, AccrualDCB) :
                                    1.0; q.Notional = (double)Nominal;
                                q.Fv = LegType == SwapLegType.Fixed ?
                                    (double)Nominal * q.DiscountFactor * (double)FixedRateOrMargin :
                                    0;
                                q.FixedRateOrMargin = (double)FixedRateOrMargin;
                                q.Notional = (double)Nominal;
                                lf.Add(q);
                            }
                        break;
                    }
                case StubType.ShortBack:
                case StubType.LongBack:
                    {
                        var currentReset = startDate;
                        while (GetNextResetDate(currentReset, true) <= endDate)
                        {
                            var q = new CashFlow
                            {
                                AccrualPeriodStart = currentReset,
                                FixingDateStart =
                                    currentReset.SubtractPeriod(FixingRollType, FixingCalendar, FixingOffset),
                                AccrualPeriodEnd = currentReset.AddPeriod(ResetRollType, ResetCalendar, ResetFrequency)
                            };
                            q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);
                            //Q.Currency = CCY;
                            q.DiscountFactor = (LegType != SwapLegType.FixedNoAccrual) && (LegType != SwapLegType.FloatNoAccrual) ?
                                q.AccrualPeriodStart.CalculateYearFraction(q.AccrualPeriodEnd, AccrualDCB) :
                                1.0;
                            q.Notional = (double)Nominal;
                            q.Fv = LegType == SwapLegType.Fixed ?
                                (double)Nominal * q.DiscountFactor * (double)FixedRateOrMargin :
                                0;
                            q.FixedRateOrMargin = (double)FixedRateOrMargin;
                            q.FlowType = LegType == SwapLegType.Fixed ? FlowType.FixedRate : FlowType.FloatRate;
                            lf.Add(q);
                            currentReset = GetNextResetDate(currentReset, false);
                        }



                        if (lf.Last().AccrualPeriodEnd != endDate)
                            if (StubType == StubType.LongBack)
                            {
                                var q = lf.Last();
                                q.AccrualPeriodEnd = endDate;
                                q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                    q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                    q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);

                            }
                            else
                            {
                                var q = new CashFlow();
                                q.AccrualPeriodStart = lf.Last().AccrualPeriodEnd;
                                q.FixingDateStart = startDate.SubtractPeriod(FixingRollType, FixingCalendar, FixingOffset);
                                q.AccrualPeriodEnd = endDate;
                                q.SettleDate = PaymentOffsetRelativeTo == OffsetRelativeToType.PeriodEnd ?
                                    q.AccrualPeriodEnd.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset) :
                                    q.AccrualPeriodStart.AddPeriod(PaymentRollType, PaymentCalendar, PaymentOffset);
                                //Q.Currency = CCY;
                                q.DiscountFactor = (LegType != SwapLegType.FixedNoAccrual) && (LegType != SwapLegType.FloatNoAccrual) ?
                                    q.AccrualPeriodStart.CalculateYearFraction(q.AccrualPeriodEnd, AccrualDCB) :
                                    1.0; q.Notional = (double)Nominal;
                                q.Fv = LegType == SwapLegType.Fixed ?
                                    (double)Nominal * q.DiscountFactor * (double)FixedRateOrMargin :
                                    0;
                                q.FixedRateOrMargin = (double)FixedRateOrMargin;
                                q.Notional = (double)Nominal;
                                lf.Add(q);
                            }
                        break;
                    }
                case StubType.LongBoth:
                case StubType.ShortBoth:
                    throw new NotImplementedException("Schedules with Both type stubs cannot be generated");
            }

            if ((NotionalExchange == ExchangeType.BackOnly) || (NotionalExchange == ExchangeType.Both))
                lf.Add(new CashFlow
                {
                    Notional = (double)Nominal * (Direction == SwapPayReceiveType.Receiver ? -1.0 : 1.0),
                    Fv = (double)Nominal * (Direction == SwapPayReceiveType.Receiver ? -1.0 : 1.0),
                    SettleDate = endDate,
                    DiscountFactor = 1.0,
                    FlowType = FlowType.FixedAmount
                });
            f.Flows = lf.OrderBy(x => x.AccrualPeriodStart).ToList();

            return f;
        }

        private DateTime GetNextResetDate(DateTime currentReset, bool fwdDirection)
        {
            if (RollDay == "IMM")
                return fwdDirection ? currentReset.GetNextImmDate() : currentReset.GetPrevImmDate();
            if (RollDay == "EOM")
                if (fwdDirection)
                {
                    var d1 = currentReset.AddPeriod(ResetRollType, ResetCalendar, ResetFrequency);
                    return d1.LastDayOfMonth().AddPeriod(RollType.P, ResetCalendar, Frequency.ZeroBd);
                }
                else
                {
                    var d1 = currentReset.SubtractPeriod(ResetRollType, ResetCalendar, ResetFrequency);
                    return d1.LastDayOfMonth().AddPeriod(RollType.P, ResetCalendar, Frequency.ZeroBd);
                }

            int rollOut;
            if (int.TryParse(RollDay, out rollOut))
                if (fwdDirection)
                {
                    var d1 = currentReset.AddPeriod(ResetRollType, ResetCalendar, ResetFrequency);
                    return new DateTime(d1.Year, d1.Month, rollOut).AddPeriod(ResetRollType, ResetCalendar, Frequency.ZeroBd);
                }
                else
                {
                    var d1 = currentReset.SubtractPeriod(ResetRollType, ResetCalendar, ResetFrequency);
                    return new DateTime(d1.Year, d1.Month, rollOut).AddPeriod(ResetRollType, ResetCalendar, Frequency.ZeroBd);
                }
            return fwdDirection ? currentReset.AddPeriod(ResetRollType, ResetCalendar, ResetFrequency) : currentReset.SubtractPeriod(ResetRollType, ResetCalendar, ResetFrequency);
        }
    }
}
