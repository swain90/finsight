// Mirrors FinSight.Domain.Enums.TransactionCategory (numbers are what the API accepts).
export const CATEGORIES: { name: string; value: number; label: string }[] = [
  { name: 'Uncategorized', value: 0, label: 'Uncategorized' },
  { name: 'HousingAndRent', value: 10, label: 'Housing & Rent' },
  { name: 'Utilities', value: 11, label: 'Utilities' },
  { name: 'GroceriesAndFood', value: 12, label: 'Groceries & Food' },
  { name: 'Transportation', value: 13, label: 'Transportation' },
  { name: 'HealthAndMedical', value: 14, label: 'Health & Medical' },
  { name: 'DiningOut', value: 20, label: 'Dining Out' },
  { name: 'Entertainment', value: 21, label: 'Entertainment' },
  { name: 'ShoppingAndClothing', value: 22, label: 'Shopping & Clothing' },
  { name: 'PersonalCare', value: 23, label: 'Personal Care' },
  { name: 'TravelAndVacation', value: 24, label: 'Travel & Vacation' },
  { name: 'DebtPayments', value: 30, label: 'Debt Payments' },
  { name: 'Savings', value: 31, label: 'Savings' },
  { name: 'Insurance', value: 32, label: 'Insurance' },
  { name: 'BankFees', value: 33, label: 'Bank Fees' },
  { name: 'Salary', value: 40, label: 'Salary' },
  { name: 'FreelanceIncome', value: 41, label: 'Freelance Income' },
  { name: 'InvestmentIncome', value: 42, label: 'Investment Income' },
  { name: 'GovernmentBenefits', value: 43, label: 'Government Benefits' },
  { name: 'PayFromCorporation', value: 44, label: 'Pay from Corporation' },
  { name: 'EducationAndWork', value: 50, label: 'Education & Work' },
  { name: 'CharitableGiving', value: 51, label: 'Charitable Giving' },
  { name: 'PetsAndAnimals', value: 52, label: 'Pets & Animals' },
  { name: 'Other', value: 99, label: 'Other' },
];

export const categoryValue = (name: string): number =>
  CATEGORIES.find((c) => c.name === name)?.value ?? 0;
