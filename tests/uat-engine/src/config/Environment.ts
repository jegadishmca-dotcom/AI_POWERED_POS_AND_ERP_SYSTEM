export type EnvironmentType = 'LOCAL' | 'UAT' | 'PROD';

export interface EnvironmentConfig {
  type: EnvironmentType;
  erpUrl: string;
  apiUrl: string;
  dbConnectionString: string;
  cashierCredentials: {
    username: string;
    password: string;
  };
}

export class Environment {
  private static instance: Environment;
  private config: EnvironmentConfig;

  private constructor() {
    const envType = (process.env.TEST_ENV || 'LOCAL').toUpperCase() as EnvironmentType;
    this.config = this.loadConfig(envType);
  }

  public static getInstance(): Environment {
    if (!Environment.instance) {
      Environment.instance = new Environment();
    }
    return Environment.instance;
  }

  public getConfig(): EnvironmentConfig {
    return this.config;
  }

  private loadConfig(type: EnvironmentType): EnvironmentConfig {
    switch (type) {
      case 'UAT':
        return {
          type: 'UAT',
          erpUrl: process.env.UAT_ERP_URL || 'https://uat.pos.applesupermarket.com',
          apiUrl: process.env.UAT_API_URL || 'https://uat.api.applesupermarket.com',
          dbConnectionString: process.env.UAT_DB_CONN || 'postgresql://uat_user:uat_pass@uat-db:5432/apple_erp_uat',
          cashierCredentials: {
            username: process.env.UAT_CASHIER_USER || 'uat_cashier',
            password: process.env.UAT_CASHIER_PASS || 'uat_pass123'
          }
        };
      case 'PROD':
        return {
          type: 'PROD',
          erpUrl: process.env.PROD_ERP_URL || 'https://pos.applesupermarket.com',
          apiUrl: process.env.PROD_API_URL || 'https://api.applesupermarket.com',
          dbConnectionString: process.env.PROD_DB_CONN || 'postgresql://prod_user:prod_pass@prod-db:5432/apple_erp_prod',
          cashierCredentials: {
            username: process.env.PROD_CASHIER_USER || 'prod_cashier',
            password: process.env.PROD_CASHIER_PASS || 'PROD_SECURE_PASS'
          }
        };
      case 'LOCAL':
      default:
        return {
          type: 'LOCAL',
          erpUrl: process.env.LOCAL_ERP_URL || 'http://localhost:3000',
          apiUrl: process.env.LOCAL_API_URL || 'http://localhost:4000',
          dbConnectionString: process.env.LOCAL_DB_CONN || 'postgresql://postgres:postgres@localhost:5432/apple_erp_local',
          cashierCredentials: {
            username: process.env.LOCAL_CASHIER_USER || 'local_cashier',
            password: process.env.LOCAL_CASHIER_PASS || 'local123'
          }
        };
    }
  }
}
